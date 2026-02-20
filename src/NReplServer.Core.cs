using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using clojure.lang;

namespace clojureCLR_nrepl
{
    public partial class NReplServer
    {
        private TcpListener listener;
        private bool running = false;
        private readonly int port;
        private readonly string host;
        private readonly Dictionary<string, NReplSession> sessions = new Dictionary<string, NReplSession>();
        private readonly bool debugComplete;
        private readonly bool debugEldoc;
        private static readonly ConcurrentDictionary<Type, CompletionCache> CompletionCacheByType =
            new ConcurrentDictionary<Type, CompletionCache>();

        private sealed class CompletionCache
        {
            public string[] StaticMethods = Array.Empty<string>();
            public string[] StaticProperties = Array.Empty<string>();
            public string[] StaticFields = Array.Empty<string>();
            public string[] InstanceMethods = Array.Empty<string>();
            public string[] InstanceProperties = Array.Empty<string>();
            public string[] InstanceFields = Array.Empty<string>();
        }

        public NReplServer(string host, int port)
        {
            this.host = host;
            this.port = port;
            debugComplete = IsEnvTrue("NREPL_DEBUG_COMPLETE");
            debugEldoc = IsEnvTrue("NREPL_DEBUG_ELDOC");
        }
        public void Start()
        {
            var ip = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host);
            listener = new TcpListener(new IPEndPoint(ip, port));
            listener.Start();
            running = true;

            Console.WriteLine($"nREPL server started on {host}:{port}");
            Console.WriteLine("Connect using:");
            Console.WriteLine($"  lein repl :connect {host}:{port}");
            Console.WriteLine("  Calva: Connect to Running nREPL Server");
            Console.WriteLine("  CIDER: cider-connect-clj");
            Console.WriteLine();

            new Thread(() =>
            {
                while (running)
                {
                    try
                    {
                        var client = listener.AcceptTcpClient();
                        new Thread(() => HandleClient(client)).Start();
                    }
                    catch (Exception e)
                    {
                        if (running)
                            Console.WriteLine($"Accept error: {e.Message}");
                    }
                }
            }).Start();
        }

        public void Stop()
        {
            running = false;
            listener?.Stop();
        }

        private void HandleClient(TcpClient client)
        {
            bool clientUsesLengthPrefix = false;
            string sessionId = null;

            try
            {
                Console.WriteLine("Client connected");
                var stream = client.GetStream();

                // Create default session
                sessionId = CreateSession();

                // Read buffer for accumulating data
                var buffer = new byte[8192];
                var accumulated = new List<byte>();

                while (running && client.Connected)
                {
                    try
                    {
                        // Read available data
                        var read = stream.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            Console.WriteLine("Client disconnected (0 bytes read)");
                            break;
                        }

                        // Add to accumulated buffer
                        accumulated.AddRange(new ArraySegment<byte>(buffer, 0, read));

                        // Try to process complete messages
                        while (accumulated.Count > 0)
                        {
                            // Check first byte to determine format
                            if (accumulated[0] >= '0' && accumulated[0] <= '9')
                            {
                                // Length-prefixed format
                                clientUsesLengthPrefix = true;

                                // Find length (read until ':')
                                int colonPos = -1;
                                for (int i = 0; i < accumulated.Count && i < 20; i++)
                                {
                                    if (accumulated[i] == ':')
                                    {
                                        colonPos = i;
                                        break;
                                    }
                                }

                                if (colonPos < 0)
                                    break; // Need more data

                                // Parse length
                                var lenStr = Encoding.UTF8.GetString(accumulated.GetRange(0, colonPos).ToArray());
                                if (!int.TryParse(lenStr, out var msgLen))
                                {
                                    // Invalid length, skip byte
                                    accumulated.RemoveAt(0);
                                    continue;
                                }

                                // Check if we have complete message
                                var totalLen = colonPos + 1 + msgLen;
                                if (accumulated.Count < totalLen)
                                    break; // Need more data

                                // Extract message
                                var msgBytes = accumulated.GetRange(colonPos + 1, msgLen).ToArray();
                                accumulated.RemoveRange(0, totalLen);

                                try
                                {
                                    var request = BencodeCodec.DecodeDict(msgBytes);
                                    var session = sessions.GetValueOrDefault(sessionId);
                                    sessionId = HandleMessage(stream, request, sessionId, clientUsesLengthPrefix, session);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Decode error: {e.Message}");
                                }
                            }
                            else if (accumulated[0] == 'd')
                            {
                                // Direct bencode - find the end
                                clientUsesLengthPrefix = false;
                                var msgEnd = FindBencodeEnd(accumulated, 0);
                                if (msgEnd > 0)
                                {
                                    var msgBytes = accumulated.GetRange(0, msgEnd).ToArray();
                                    accumulated.RemoveRange(0, msgEnd);

                                    try
                                    {
                                        var request = BencodeCodec.DecodeDict(msgBytes);
                                        var session = sessions.GetValueOrDefault(sessionId);
                                        sessionId = HandleMessage(stream, request, sessionId, clientUsesLengthPrefix, session);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"Decode error: {e.Message}");
                                    }
                                }
                                else
                                {
                                    break; // Need more data
                                }
                            }
                            else
                            {
                                // Unknown format, skip byte
                                accumulated.RemoveAt(0);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (running)
                        {
                            Console.WriteLine($"Client error: {e.Message}");
                        }
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Client handler error: {e.Message}");
            }
            finally
            {
                Console.WriteLine("Client disconnected");
                client.Close();
            }
        }

        private int FindBencodeEnd(List<byte> data, int start)
        {
            int depth = 0;
            bool inString = false;
            int stringLen = 0;
            bool inInt = false;

            for (int i = start; i < data.Count; i++)
            {
                var b = data[i];

                if (inString)
                {
                    stringLen--;
                    if (stringLen == 0)
                        inString = false;
                    continue;
                }

                if (inInt)
                {
                    if (b == 'e')
                        inInt = false;
                    continue;
                }

                if (b == 'd' || b == 'l')
                {
                    if (depth == 0)
                        start = i;
                    depth++;
                }
                else if (b == 'e' && depth > 0)
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
                else if (b == 'i')
                {
                    inInt = true;
                }
                else if (b >= '0' && b <= '9')
                {
                    // Parse string length
                    var lenBytes = new List<byte>();
                    int j = i;
                    while (j < data.Count && data[j] != (byte)':')
                    {
                        lenBytes.Add(data[j]);
                        j++;
                    }
                    if (j >= data.Count)
                        return -1; // Incomplete

                    var lenStr = Encoding.UTF8.GetString(lenBytes.ToArray());
                    if (!int.TryParse(lenStr, out stringLen))
                        return -1; // Invalid

                    // Handle empty string (0:) - skip directly, don't enter string mode
                    if (stringLen == 0)
                    {
                        inString = false;
                    }
                    else
                    {
                        inString = true;
                    }
                    i = j; // Skip to colon
                }
            }

            return -1; // Incomplete
        }

        private string GetId(Dictionary<string, object> request)
        {
            var idObj = request.GetValueOrDefault("id");
            if (idObj is long l) return l.ToString();
            if (idObj is int i) return i.ToString();
            return idObj as string ?? "unknown";
        }

        private string HandleMessage(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, NReplSession session)
        {
            var op = request.GetValueOrDefault("op") as string;
            var id = GetId(request);

            Console.WriteLine($"Received op: '{op}', id: '{id}', useLengthPrefix: {useLengthPrefix}");

            switch (op)
            {
                case "describe":
                    SendResponse(stream, CreateDescribeResponse(id, sessionId), useLengthPrefix);
                    break;
                case "clone":
                    return HandleClone(stream, request, useLengthPrefix);
                case "eval":
                    HandleEval(stream, request, sessionId, useLengthPrefix, session);
                    break;
                case "close":
                    SendResponse(stream, CreateResponse(id, sessionId, new List<string> { "done" }), useLengthPrefix);
                    break;
                case "ls-sessions":
                    HandleLsSessions(stream, request, sessionId, useLengthPrefix);
                    break;
                case "interrupt":
                    HandleInterrupt(stream, request, sessionId, useLengthPrefix);
                    break;
                case "load-file":
                    HandleLoadFile(stream, request, sessionId, useLengthPrefix);
                    break;
                case "stdin":
                    // Stdin not supported yet, send done
                    SendResponse(stream, CreateResponse(id, sessionId, new List<string> { "done" }), useLengthPrefix);
                    break;
                case "complete":
                    HandleComplete(stream, request, sessionId, useLengthPrefix, session);
                    break;
                case "info":
                    HandleInfo(stream, request, sessionId, useLengthPrefix, session);
                    break;
                case "eldoc":
                    HandleEldoc(stream, request, sessionId, useLengthPrefix, session);
                    break;
                default:
                    Console.WriteLine($"Unknown op: {op}");
                    SendResponse(stream, CreateResponse(id, sessionId, new List<string> { "done", "unknown-op" }), useLengthPrefix);
                    break;
            }
            return sessionId;
        }

        private Dictionary<string, object> CreateDescribeResponse(string id, string sessionId)
        {
            return new Dictionary<string, object>
            {
                ["id"] = id,
                ["session"] = sessionId,
                ["ops"] = new Dictionary<string, object>
                {
                    ["eval"] = new Dictionary<string, object>(),
                    ["clone"] = new Dictionary<string, object>(),
                    ["describe"] = new Dictionary<string, object>(),
                    ["close"] = new Dictionary<string, object>(),
                    ["ls-sessions"] = new Dictionary<string, object>(),
                    ["interrupt"] = new Dictionary<string, object>(),
                    ["load-file"] = new Dictionary<string, object>(),
                    ["stdin"] = new Dictionary<string, object>(),
                    ["complete"] = new Dictionary<string, object>(),
                    ["info"] = new Dictionary<string, object>(),
                    ["eldoc"] = new Dictionary<string, object>()
                },
                ["versions"] = new Dictionary<string, object>
                {
                    ["nrepl"] = new Dictionary<string, object>
                    {
                        ["major"] = "0",
                        ["minor"] = "8",
                        ["incremental"] = "1"
                    }
                },
                ["status"] = new List<string> { "done" }
            };
        }

        private Dictionary<string, object> CreateResponse(string id, string sessionId, List<string> status)
        {
            return new Dictionary<string, object>
            {
                ["id"] = id,
                ["session"] = sessionId,
                ["status"] = status
            };
        }

        private void SendResponse(NetworkStream stream, Dictionary<string, object> response, bool useLengthPrefix)
        {
            var bytes = BencodeCodec.Encode(response);

            // Match client's format
            if (useLengthPrefix)
            {
                // 4-byte big-endian length prefix (standard nREPL)
                var lenBytes = BitConverter.GetBytes(bytes.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(lenBytes);
                stream.Write(lenBytes, 0, 4);
            }
            // else: pure bencode (no prefix)

            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();

            Console.WriteLine($"Sent response: {bytes.Length} bytes (useLengthPrefix={useLengthPrefix})");

            // Small delay to ensure client receives data
            Thread.Sleep(10);
        }

        private string CreateSession()
        {
            var id = Guid.NewGuid().ToString();
            sessions[id] = new NReplSession { Id = id };
            return id;
        }

    }
}
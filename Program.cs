// ClojureCLR nREPL Server - C# implementation

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
    public static class Bencode
    {
        public static byte[] Encode(object obj)
        {
            using (var ms = new MemoryStream())
            {
                EncodeObject(ms, obj);
                return ms.ToArray();
            }
        }

        private static void EncodeObject(MemoryStream ms, object obj)
        {
            switch (obj)
            {
                case string s:
                    EncodeString(ms, s);
                    break;
                case int i:
                    EncodeInt(ms, i);
                    break;
                case long l:
                    EncodeInt(ms, l);
                    break;
                case Keyword k:
                    EncodeString(ms, k.Name);
                    break;
                case IPersistentVector vec:
                    EncodeList(ms, vec);
                    break;
                case IPersistentMap map:
                    EncodeDict(ms, map);
                    break;
                case List<string> strList:
                    EncodeStringList(ms, strList);
                    break;
                case List<object> list:
                    EncodeGenericList(ms, list);
                    break;
                case Dictionary<string, object> dict:
                    EncodeGenericDict(ms, dict);
                    break;
                case System.Collections.IList list:
                    // Handle any list type (List<Dictionary<string, object>>, etc.)
                    EncodeIList(ms, list);
                    break;
                default:
                    EncodeString(ms, obj?.ToString() ?? "nil");
                    break;
            }
        }

        private static void EncodeIList(MemoryStream ms, System.Collections.IList list)
        {
            ms.WriteByte((byte)'l');
            foreach (var item in list)
            {
                EncodeObject(ms, item);
            }
            ms.WriteByte((byte)'e');
        }

        private static void EncodeString(MemoryStream ms, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var lenBytes = Encoding.UTF8.GetBytes(bytes.Length.ToString());
            ms.Write(lenBytes, 0, lenBytes.Length);
            ms.WriteByte((byte)':');
            ms.Write(bytes, 0, bytes.Length);
        }

        private static void EncodeInt(MemoryStream ms, long n)
        {
            var bytes = Encoding.UTF8.GetBytes($"i{n}e");
            ms.Write(bytes, 0, bytes.Length);
        }

        private static void EncodeList(MemoryStream ms, IPersistentVector vec)
        {
            ms.WriteByte((byte)'l');
            for (int i = 0; i < vec.count(); i++)
            {
                EncodeObject(ms, vec.nth(i));
            }
            ms.WriteByte((byte)'e');
        }

        private static void EncodeStringList(MemoryStream ms, List<string> list)
        {
            ms.WriteByte((byte)'l');
            foreach (var item in list)
            {
                EncodeString(ms, item);
            }
            ms.WriteByte((byte)'e');
        }

        private static void EncodeGenericList(MemoryStream ms, List<object> list)
        {
            ms.WriteByte((byte)'l');
            foreach (var item in list)
            {
                EncodeObject(ms, item);
            }
            ms.WriteByte((byte)'e');
        }

        private static void EncodeDict(MemoryStream ms, IPersistentMap map)
        {
            ms.WriteByte((byte)'d');
            foreach (var entry in map)
            {
                var key = ((Keyword)entry.key()).Name;
                EncodeString(ms, key);
                EncodeObject(ms, entry.val());
            }
            ms.WriteByte((byte)'e');
        }

        private static void EncodeGenericDict(MemoryStream ms, Dictionary<string, object> dict)
        {
            ms.WriteByte((byte)'d');
            foreach (var entry in dict)
            {
                EncodeString(ms, entry.Key);
                EncodeObject(ms, entry.Value);
            }
            ms.WriteByte((byte)'e');
        }

        public static Dictionary<string, object> DecodeDict(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                return ReadDict(ms);
            }
        }

        public static Dictionary<string, object> DecodeDict(Stream stream)
        {
            return ReadDict(stream);
        }

        private static Dictionary<string, object> ReadDict(Stream stream)
        {
            var b = stream.ReadByte();
            if (b != 'd')
                throw new InvalidDataException($"Expected 'd', got '{(char)b}' ({b})");

            var result = new Dictionary<string, object>();

            while (true)
            {
                var peek = stream.ReadByte();
                if (peek == 'e' || peek == -1)
                    break;

                stream.Seek(-1, SeekOrigin.Current);
                var key = ReadString(stream);
                var value = ReadValue(stream);
                result[key] = value;
            }

            return result;
        }

        private static object ReadValue(Stream stream)
        {
            var b = stream.ReadByte();
            if (b == -1)
                throw new EndOfStreamException();

            stream.Seek(-1, SeekOrigin.Current);

            if (b == 'd')
                return ReadDict(stream);
            if (b == 'l')
                return ReadList(stream);
            if (b == 'i')
                return ReadInt(stream);

            return ReadString(stream);
        }

        private static List<object> ReadList(Stream stream)
        {
            var b = stream.ReadByte();
            if (b != 'l')
                throw new InvalidDataException($"Expected 'l', got '{(char)b}'");

            var result = new List<object>();

            while (true)
            {
                var peek = stream.ReadByte();
                if (peek == 'e' || peek == -1)
                    break;

                stream.Seek(-1, SeekOrigin.Current);
                result.Add(ReadValue(stream));
            }

            return result;
        }

        private static long ReadInt(Stream stream)
        {
            var b = stream.ReadByte();
            if (b != 'i')
                throw new InvalidDataException($"Expected 'i', got '{(char)b}'");

            var numBytes = new MemoryStream();
            while (true)
            {
                b = stream.ReadByte();
                if (b == 'e' || b == -1)
                    break;
                numBytes.WriteByte((byte)b);
            }

            return long.Parse(Encoding.UTF8.GetString(numBytes.ToArray()));
        }

        private static string ReadString(Stream stream)
        {
            var lenBytes = new MemoryStream();
            while (true)
            {
                var b = stream.ReadByte();
                if (b == ':' || b == -1)
                    break;
                lenBytes.WriteByte((byte)b);
            }

            var len = int.Parse(Encoding.UTF8.GetString(lenBytes.ToArray()));
            var buf = new byte[len];
            var read = stream.Read(buf, 0, len);
            if (read < len)
                throw new EndOfStreamException($"Expected {len} bytes, got {read}");

            return Encoding.UTF8.GetString(buf);
        }
    }

    public class NReplServer
    {
        private TcpListener listener;
        private bool running = false;
        private readonly int port;
        private readonly string host;
        private readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();
        private readonly bool debugComplete;
        private readonly bool debugEldoc;
        private static readonly ConcurrentDictionary<Type, MemberCache> MemberCacheByType =
            new ConcurrentDictionary<Type, MemberCache>();

        private sealed class MemberCache
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
                                    var request = Bencode.DecodeDict(msgBytes);
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
                                        var request = Bencode.DecodeDict(msgBytes);
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

        private string HandleMessage(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, Session session)
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

        private string HandleClone(NetworkStream stream, Dictionary<string, object> request, bool useLengthPrefix)
        {
            var id = GetId(request);
            var newSessionId = CreateSession();

            Console.WriteLine($"Created new session: {newSessionId}");

            var response = new Dictionary<string, object>
            {
                ["id"] = id,
                ["new-session"] = newSessionId,
                ["status"] = new List<string> { "done" }
            };
            SendResponse(stream, response, useLengthPrefix);
            return newSessionId;
        }

        private void HandleEval(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, Session session)
        {
            var id = GetId(request);
            var code = request.GetValueOrDefault("code") as string;
            var nsParam = request.GetValueOrDefault("ns") as string;

            Console.WriteLine($"Evaluating: {code}");

            try
            {
                // If client provides ns, honor it for this session
                if (!string.IsNullOrEmpty(nsParam) && session != null)
                {
                    try
                    {
                        var findNs = RT.var("clojure.core", "find-ns");
                        var ns = findNs.invoke(Symbol.create(nsParam)) as Namespace;
                        if (ns == null)
                        {
                            // Try require first
                            try
                            {
                                var require = RT.var("clojure.core", "require");
                                require.invoke(Symbol.create(nsParam));
                                ns = findNs.invoke(Symbol.create(nsParam)) as Namespace;
                            }
                            catch { }

                            if (ns == null)
                            {
                                ns = Namespace.findOrCreate(Symbol.create(nsParam));
                            }
                        }

                        // Ensure clojure.core is referred for usability
                        try
                        {
                            var refer = RT.var("clojure.core", "refer");
                            Var.pushThreadBindings(RT.map(RT.CurrentNSVar, ns));
                            try
                            {
                                refer.invoke(Symbol.create("clojure.core"));
                            }
                            finally
                            {
                                Var.popThreadBindings();
                            }
                        }
                        catch { }

                        session.CurrentNamespace = ns;
                    }
                    catch { }
                }

                var result = EvaluateClojure(code, session);
                Console.WriteLine($"Result: {result}");
                var currentNs = session?.CurrentNamespace ?? RT.CurrentNSVar.deref() as Namespace;
                var nsName = currentNs?.Name?.Name ?? "user";

                // Send value response
                var valueResponse = new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["value"] = result,
                    ["ns"] = nsName
                };
                Console.WriteLine("Sending value response...");
                SendResponse(stream, valueResponse, useLengthPrefix);

                // Send done status
                var doneResponse = new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["status"] = new List<string> { "done" }
                };
                Console.WriteLine("Sending done response...");
                SendResponse(stream, doneResponse, useLengthPrefix);
                Console.WriteLine("Eval complete");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Eval error: {e.Message}");
                Console.WriteLine($"Stack: {e.StackTrace}");

                // Send error response
                var errorResponse = new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["ex"] = e.GetType().Name,
                    ["root-ex"] = e.GetType().Name,
                    ["err"] = e.Message + "\n",
                    ["status"] = new List<string> { "eval-error" }
                };
                SendResponse(stream, errorResponse, useLengthPrefix);

                // Send done
                var doneResponse = new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["status"] = new List<string> { "done" }
                };
                SendResponse(stream, doneResponse, useLengthPrefix);
            }
        }

        private string EvaluateClojure(string code, Session session)
        {
            try
            {
                var readString = RT.var("clojure.core", "read-string");
                var prStr = RT.var("clojure.core", "pr-str");
                var findNs = RT.var("clojure.core", "find-ns");

                var form = readString.invoke(code);

                // Handle in-ns specially
                if (form is ISeq seq && seq.first() is Symbol sym && sym.Name == "in-ns")
                {
                    var nsArg = seq.next().first();
                    // Handle (quote ns) form
                    if (nsArg is ISeq quoteSeq && quoteSeq.first() is Symbol quoteSym && quoteSym.Name == "quote")
                    {
                        nsArg = quoteSeq.next().first();
                    }

                    // Find or create the namespace
                    Namespace ns = findNs.invoke(nsArg) as Namespace;
                    if (ns == null)
                    {
                        // Try to require the namespace first (it might be a library)
                        try
                        {
                            var require = RT.var("clojure.core", "require");
                            require.invoke(nsArg);
                            ns = findNs.invoke(nsArg) as Namespace;
                        }
                        catch { }

                        // If still not found, create it
                        if (ns == null)
                        {
                            ns = Namespace.findOrCreate((Symbol)nsArg);
                        }
                    }

                    // Always refer clojure.core in the new namespace for usability
                    try
                    {
                        var refer = RT.var("clojure.core", "refer");
                        Var.pushThreadBindings(RT.map(RT.CurrentNSVar, ns));
                        try
                        {
                            refer.invoke(Symbol.create("clojure.core"));
                        }
                        finally
                        {
                            Var.popThreadBindings();
                        }
                    }
                    catch { }

                    session.CurrentNamespace = ns;
                    return prStr.invoke(ns) as string;
                }

                // Setup namespace binding for evaluation
                if (session?.CurrentNamespace != null)
                {
                    Var.pushThreadBindings(RT.map(RT.CurrentNSVar, session.CurrentNamespace));
                }

                try
                {
                    var eval = RT.var("clojure.core", "eval");
                    var result = eval.invoke(form);
                    return prStr.invoke(result) as string;
                }
                finally
                {
                    if (session?.CurrentNamespace != null)
                    {
                        Var.popThreadBindings();
                    }
                }
            }
            catch (Exception e)
            {
                return $"#error {{:message \"{e.Message}\"}}";
            }
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
            var bytes = Bencode.Encode(response);

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

        private void HandleLsSessions(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix)
        {
            var id = GetId(request);
            var sessionList = new List<string>(sessions.Keys);

            SendResponse(stream, new Dictionary<string, object>
            {
                ["id"] = id,
                ["session"] = sessionId,
                ["sessions"] = sessionList,
                ["status"] = new List<string> { "done" }
            }, useLengthPrefix);
        }

        private void HandleInterrupt(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix)
        {
            var id = GetId(request);
            // Interrupt not fully supported, just acknowledge
            SendResponse(stream, new Dictionary<string, object>
            {
                ["id"] = id,
                ["session"] = sessionId,
                ["status"] = new List<string> { "done" }
            }, useLengthPrefix);
        }

        private void HandleLoadFile(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix)
        {
            var id = GetId(request);
            var filePath = request.GetValueOrDefault("file-path") as string;
            var fileName = request.GetValueOrDefault("file-name") as string;
            var fileContent = request.GetValueOrDefault("file") as string;

            Console.WriteLine($"Load file: {fileName} at {filePath}");

            try
            {
                if (string.IsNullOrEmpty(fileContent) && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    fileContent = File.ReadAllText(filePath);
                }

                if (string.IsNullOrEmpty(fileContent))
                {
                    SendResponse(stream, new Dictionary<string, object>
                    {
                        ["id"] = id,
                        ["session"] = sessionId,
                        ["value"] = "nil",
                        ["status"] = new List<string> { "done" }
                    }, useLengthPrefix);
                    return;
                }

                var session = sessions.GetValueOrDefault(sessionId);
                Namespace ns = session?.CurrentNamespace ?? RT.CurrentNSVar.deref() as Namespace;

                if (ns != null)
                {
                    Var.pushThreadBindings(RT.map(RT.CurrentNSVar, ns));
                }

                string resultValue;
                Namespace resultNs = ns;
                try
                {
                    var loadString = RT.var("clojure.core", "load-string");
                    var prStr = RT.var("clojure.core", "pr-str");
                    var result = loadString.invoke(fileContent);
                    resultValue = prStr.invoke(result) as string;
                    resultNs = RT.CurrentNSVar.deref() as Namespace ?? ns;
                }
                finally
                {
                    if (ns != null)
                    {
                        Var.popThreadBindings();
                    }
                }

                if (session != null && resultNs != null)
                {
                    session.CurrentNamespace = resultNs;
                }

                SendResponse(stream, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["value"] = resultValue ?? "nil",
                    ["ns"] = resultNs?.Name?.Name ?? "user",
                    ["status"] = new List<string> { "done" }
                }, useLengthPrefix);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Load file error: {e.Message}");
                SendResponse(stream, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["status"] = new List<string> { "eval-error", "done" }
                }, useLengthPrefix);
            }
        }

        private void HandleComplete(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, Session session)
        {
            var id = GetId(request);
            var prefix = request.GetValueOrDefault("symbol") as string
                ?? request.GetValueOrDefault("prefix") as string
                ?? request.GetValueOrDefault("sym") as string
                ?? request.GetValueOrDefault("text") as string
                ?? "";
            var nsName = request.GetValueOrDefault("ns") as string;

            if (string.IsNullOrEmpty(prefix))
            {
                prefix = ExtractPrefixFromRequest(request);
            }

            if (debugComplete)
            {
                Console.WriteLine($"Complete request fields: {FormatCompleteDebug(request)}");
            }
            Console.WriteLine($"Complete request: prefix='{prefix}', ns='{nsName}'");

            try
            {
                var dotCompletions = TryGetDotCompletions(request, nsName, session);
                if (dotCompletions != null && dotCompletions.Count > 0)
                {
                    SendResponse(stream, new Dictionary<string, object>
                    {
                        ["id"] = id,
                        ["session"] = sessionId,
                        ["completions"] = dotCompletions,
                        ["status"] = new List<string> { "done" }
                    }, useLengthPrefix);
                    return;
                }

                if (!string.IsNullOrEmpty(prefix) && prefix.StartsWith("."))
                {
                    var dotPrefixCompletions = TryGetDotCompletionsFromPrefix(prefix, request, nsName, session);
                    if (dotPrefixCompletions != null && dotPrefixCompletions.Count > 0)
                    {
                        SendResponse(stream, new Dictionary<string, object>
                        {
                            ["id"] = id,
                            ["session"] = sessionId,
                            ["completions"] = dotPrefixCompletions,
                            ["status"] = new List<string> { "done" }
                        }, useLengthPrefix);
                        return;
                    }
                }

                var completions = GetCompletions(prefix, nsName, session);

                SendResponse(stream, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["completions"] = completions,
                    ["status"] = new List<string> { "done" }
                }, useLengthPrefix);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Complete error: {e.Message}");
                SendResponse(stream, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["completions"] = new List<object>(),
                    ["status"] = new List<string> { "done" }
                }, useLengthPrefix);
            }
        }

        private List<Dictionary<string, object>> GetCompletions(string prefix, string nsName, Session session)
        {
            var completions = new List<Dictionary<string, object>>();
            var prefixLower = prefix.ToLowerInvariant();

            // Get the namespace to search in
            var ns = ResolveNamespace(nsName, session);

            if (ns == null) return completions;

            // Use ns-map to get all mappings
            try
            {
                var nsMap = RT.var("clojure.core", "ns-map");
                var mappings = nsMap.invoke(ns) as IPersistentMap;

                if (mappings != null)
                {
                    foreach (var entry in mappings)
                    {
                        var name = entry.key().ToString();
                        if (name.StartsWith("__")) continue; // Skip internal vars
                        if (!name.ToLowerInvariant().StartsWith(prefixLower)) continue;

                        var type = "var";
                        var doc = "";
                        var valNs = ns.Name.Name;

                        var val = entry.val();
                        if (val is Var v)
                        {
                            // Check if it's a macro
                            try
                            {
                                var meta = v.meta() as IPersistentMap;
                                if (meta != null)
                                {
                                    var macroVal = meta.valAt(Keyword.intern("macro"));
                                    if (macroVal != null && macroVal.ToString() == "true")
                                        type = "macro";

                                    var docVal = meta.valAt(Keyword.intern("doc"));
                                    if (docVal != null) doc = docVal.ToString();

                                    if (v.Namespace != null && v.Namespace.Name != null)
                                        valNs = v.Namespace.Name.Name;
                                }
                            }
                            catch { }
                        }
                        else if (val is Type)
                        {
                            type = "class";
                            valNs = "";
                        }

                        completions.Add(new Dictionary<string, object>
                        {
                            ["candidate"] = name,
                            ["type"] = type,
                            ["ns"] = valNs,
                            ["doc"] = doc
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ns-map error: {e.Message}");
            }

            // 4. If prefix contains a namespace alias (e.g., "str/join")
            if (prefix.Contains('/'))
            {
                var parts = prefix.Split('/', 2);
                var alias = parts[0];
                var varPrefix = parts[1].ToLowerInvariant();

                // Try static member completions for CLR types (e.g., Enumerable/Where)
                try
                {
                    var resolvedType = TryResolveType(ns, alias);
                    if (resolvedType == null && session?.CurrentNamespace != null && session.CurrentNamespace != ns)
                    {
                        resolvedType = TryResolveType(session.CurrentNamespace, alias);
                    }
                    if (resolvedType != null)
                    {
                        AddTypeCompletions(completions, alias, varPrefix, resolvedType);
                    }
                }
                catch { }

                // Try to find the aliased namespace
                Namespace aliasedNs = null;
                try
                {
                    var nsAliases = RT.var("clojure.core", "ns-aliases");
                    var aliases = nsAliases.invoke(ns) as IPersistentMap;
                    if (aliases != null)
                    {
                        foreach (var entry in aliases)
                        {
                            if (entry.key().ToString() == alias)
                            {
                                aliasedNs = entry.val() as Namespace;
                                break;
                            }
                        }
                    }
                }
                catch { }

                // Also check if it's a namespace name directly
                if (aliasedNs == null)
                {
                    try
                    {
                        // Try to require the namespace first (it might not be loaded)
                        var require = RT.var("clojure.core", "require");
                        require.invoke(Symbol.create(alias));
                    }
                    catch { }

                    try
                    {
                        var findNs = RT.var("clojure.core", "find-ns");
                        aliasedNs = findNs.invoke(Symbol.create(alias)) as Namespace;
                    }
                    catch { }
                }

                // Try common namespace shorthand mappings
                if (aliasedNs == null)
                {
                    var shorthandNs = GetFullNamespaceName(alias);
                    if (!string.IsNullOrEmpty(shorthandNs))
                    {
                        try
                        {
                            var require = RT.var("clojure.core", "require");
                            require.invoke(Symbol.create(shorthandNs));
                        }
                        catch { }

                        try
                        {
                            var findNs = RT.var("clojure.core", "find-ns");
                            aliasedNs = findNs.invoke(Symbol.create(shorthandNs)) as Namespace;
                        }
                        catch { }
                    }
                }

                if (aliasedNs != null)
                {
                    try
                    {
                        var nsPublics = RT.var("clojure.core", "ns-publics");
                        var publics = nsPublics.invoke(aliasedNs) as IPersistentMap;
                            if (publics != null)
                        {
                            foreach (var entry in publics)
                            {
                                var name = entry.key().ToString();
                                if (name.StartsWith("__")) continue;
                                if (!name.ToLowerInvariant().StartsWith(varPrefix)) continue;

                                var type = entry.val() is Var ? "var" : "class";

                                completions.Add(new Dictionary<string, object>
                                {
                                    ["candidate"] = $"{alias}/{name}",
                                    ["type"] = type,
                                    ["ns"] = aliasedNs.Name?.Name ?? alias,
                                    ["doc"] = ""
                                });
                            }
                        }
                    }
                    catch { }
                }
            }

            // Sort by candidate name
            completions.Sort((a, b) =>
                string.Compare(a["candidate"].ToString(), b["candidate"].ToString(), StringComparison.Ordinal));

            return completions;
        }

        private Type TryResolveType(Namespace ns, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // Try resolving from current namespace (imported types)
            try
            {
                var nsResolve = RT.var("clojure.core", "ns-resolve");
                var resolved = nsResolve.invoke(ns, Symbol.create(name));
                if (resolved is Type t) return t;
                if (resolved is Var v)
                {
                    var val = v.deref();
                    if (val is Type vt) return vt;
                }
            }
            catch { }

            // Try full name lookup
            try
            {
                var t = Type.GetType(name, false);
                if (t != null) return t;
            }
            catch { }

            // Search loaded assemblies
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType(name, false, false);
                    if (t != null) return t;
                }
            }
            catch { }

            // Heuristic: try loading assembly by namespace prefix
            try
            {
                var parts = name.Split('.');
                if (parts.Length >= 2)
                {
                    var candidates = new List<string>
                    {
                        $"{parts[0]}.{parts[1]}",
                        parts[0]
                    };

                    foreach (var asmName in candidates)
                    {
                        try
                        {
                            var asm = Assembly.Load(new AssemblyName(asmName));
                            var t = asm.GetType(name, false, false);
                            if (t != null) return t;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return null;
        }

        private MemberCache GetMemberCache(Type type)
        {
            return MemberCacheByType.GetOrAdd(type, BuildMemberCache);
        }

        private MemberCache BuildMemberCache(Type type)
        {
            var cache = new MemberCache
            {
                StaticMethods = GetMethodNames(type, BindingFlags.Public | BindingFlags.Static),
                StaticProperties = GetPropertyNames(type, BindingFlags.Public | BindingFlags.Static),
                StaticFields = GetFieldNames(type, BindingFlags.Public | BindingFlags.Static),
                InstanceMethods = GetMethodNames(type, BindingFlags.Public | BindingFlags.Instance),
                InstanceProperties = GetPropertyNames(type, BindingFlags.Public | BindingFlags.Instance),
                InstanceFields = GetFieldNames(type, BindingFlags.Public | BindingFlags.Instance)
            };

            return cache;
        }

        private string[] GetMethodNames(Type type, BindingFlags flags)
        {
            try
            {
                var methods = type.GetMethods(flags);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var method in methods)
                {
                    if (method.IsSpecialName) continue; // skip get_/set_/op_*
                    names.Add(method.Name);
                }
                return new List<string>(names).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private string[] GetPropertyNames(Type type, BindingFlags flags)
        {
            try
            {
                var properties = type.GetProperties(flags);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in properties)
                {
                    names.Add(prop.Name);
                }
                return new List<string>(names).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private string[] GetFieldNames(Type type, BindingFlags flags)
        {
            try
            {
                var fields = type.GetFields(flags);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in fields)
                {
                    names.Add(field.Name);
                }
                return new List<string>(names).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private void AddMemberCompletions(List<Dictionary<string, object>> completions, string candidatePrefix, string memberPrefixLower, string[] members, string memberType, string nsName, HashSet<string> seen)
        {
            if (members == null || members.Length == 0) return;
            foreach (var name in members)
            {
                if (!name.ToLowerInvariant().StartsWith(memberPrefixLower)) continue;
                if (!seen.Add(name)) continue;

                completions.Add(new Dictionary<string, object>
                {
                    ["candidate"] = $"{candidatePrefix}{name}",
                    ["type"] = memberType,
                    ["ns"] = nsName,
                    ["doc"] = ""
                });
            }
        }

        private void AddTypeCompletions(List<Dictionary<string, object>> completions, string typeAlias, string memberPrefixLower, Type type)
        {
            var cache = GetMemberCache(type);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nsName = type.FullName ?? typeAlias;
            var prefix = $"{typeAlias}/";

            AddMemberCompletions(completions, prefix, memberPrefixLower, cache.StaticMethods, "method", nsName, seen);
            AddMemberCompletions(completions, prefix, memberPrefixLower, cache.StaticProperties, "property", nsName, seen);
            AddMemberCompletions(completions, prefix, memberPrefixLower, cache.StaticFields, "field", nsName, seen);
        }

        private List<Dictionary<string, object>> TryGetDotCompletions(Dictionary<string, object> request, string nsName, Session session)
        {
            var slice = GetContextSlice(request);
            if (string.IsNullOrEmpty(slice)) return null;

            if (!TryParseDotContext(slice, out var receiver, out var memberPrefix)) return null;

            var ns = ResolveNamespace(nsName, session);
            if (ns == null) return null;

            var completions = new List<Dictionary<string, object>>();
            var prefixLower = (memberPrefix ?? "").ToLowerInvariant();

            try
            {
                // Resolve receiver symbol to value/type
                var nsResolve = RT.var("clojure.core", "ns-resolve");
                var resolved = nsResolve.invoke(ns, Symbol.create(receiver));

                if (resolved is Var v)
                {
                    var val = v.deref();
                    if (val is Type t)
                    {
                        AddTypeCompletions(completions, receiver, prefixLower, t);
                        return completions;
                    }
                    if (val != null)
                    {
                        AddInstanceCompletions(completions, prefixLower, val.GetType());
                        return completions;
                    }
                }
                else if (resolved is Type t)
                {
                    AddTypeCompletions(completions, receiver, prefixLower, t);
                    return completions;
                }
            }
            catch { }

            // Fallback: try resolving receiver as type name
            try
            {
                var t = TryResolveType(ns, receiver);
                if (t != null)
                {
                    AddTypeCompletions(completions, receiver, prefixLower, t);
                    return completions;
                }
            }
            catch { }

            return null;
        }

        private Dictionary<string, object> TryGetDotEldoc(Dictionary<string, object> request, string symbol, string nsName, Session session)
        {
            var ns = ResolveNamespace(nsName, session);
            if (ns == null) return null;

            string receiver;
            string memberPrefix;
            var slice = GetContextSlice(request);

            if (!string.IsNullOrEmpty(slice) && TryParseDotContext(slice, out receiver, out memberPrefix))
            {
                // parsed from slice
            }
            else
            {
                receiver = "";
                memberPrefix = "";

                var line = request.GetValueOrDefault("line") as string;
                var buffer = request.GetValueOrDefault("buffer") as string
                    ?? request.GetValueOrDefault("code") as string
                    ?? request.GetValueOrDefault("text") as string;

                if (!string.IsNullOrEmpty(line) && TryParseDotContextAny(line, out receiver, out memberPrefix))
                {
                    // parsed from full line
                }
                else if (!string.IsNullOrEmpty(buffer) && TryParseDotContextAny(buffer, out receiver, out memberPrefix))
                {
                    // parsed from buffer
                }
                else
                {
                    if (!TryGetContextReceiver(request, out receiver))
                    {
                        receiver = "";
                    }

                    if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("."))
                    {
                        memberPrefix = symbol.TrimStart('.');
                    }
                }
            }

            if (string.IsNullOrEmpty(memberPrefix)) return null;

            Type type = null;
            bool isStatic = false;

            if (!string.IsNullOrEmpty(receiver))
            {
                try
                {
                    var nsResolve = RT.var("clojure.core", "ns-resolve");
                    var resolved = nsResolve.invoke(ns, Symbol.create(receiver));
                    if (resolved is Var v)
                    {
                        var val = v.deref();
                        if (val is Type vt)
                        {
                            type = vt;
                            isStatic = true;
                        }
                        else if (val != null)
                        {
                            type = val.GetType();
                            isStatic = false;
                        }
                    }
                    else if (resolved is Type rt)
                    {
                        type = rt;
                        isStatic = true;
                    }
                }
                catch { }

                if (type == null)
                {
                    try
                    {
                        type = TryResolveType(ns, receiver);
                        if (type != null) isStatic = true;
                    }
                    catch { }
                }
            }

            if (type == null) return null;

            var result = new Dictionary<string, object>
            {
                ["ns"] = type.FullName ?? type.Name,
                ["symbol"] = memberPrefix
            };

            var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            var eldocLists = new List<object>();

            try
            {
                foreach (var method in type.GetMethods(flags))
                {
                    if (method.IsSpecialName) continue;
                    if (!method.Name.StartsWith(memberPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var args = new List<string>();
                    foreach (var p in method.GetParameters())
                    {
                        args.Add(FormatParameter(p));
                    }
                    eldocLists.Add(args);
                }
            }
            catch { }

            if (eldocLists.Count > 0)
            {
                result["type"] = "method";
                result["eldoc"] = eldocLists;
                return result;
            }

            // Try property/field names for eldoc
            try
            {
                foreach (var prop in type.GetProperties(flags))
                {
                    if (!prop.Name.StartsWith(memberPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    eldocLists.Add(new List<string>());
                }
                if (eldocLists.Count > 0)
                {
                    result["type"] = "property";
                    result["eldoc"] = eldocLists;
                    return result;
                }
            }
            catch { }

            try
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (!field.Name.StartsWith(memberPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    eldocLists.Add(new List<string>());
                }
                if (eldocLists.Count > 0)
                {
                    result["type"] = "field";
                    result["eldoc"] = eldocLists;
                    return result;
                }
            }
            catch { }

            return null;
        }

        private List<Dictionary<string, object>> TryGetDotCompletionsFromPrefix(string prefix, Dictionary<string, object> request, string nsName, Session session)
        {
            var ns = ResolveNamespace(nsName, session);
            if (ns == null) return null;

            if (!TryGetContextReceiver(request, out var receiver)) return null;
            if (string.IsNullOrEmpty(receiver)) return null;

            var memberPrefix = prefix.TrimStart('.');
            var completions = new List<Dictionary<string, object>>();
            var prefixLower = memberPrefix.ToLowerInvariant();

            try
            {
                if (TryResolveReceiver(ns, receiver, out var type, out var isStatic, out var instanceType))
                {
                    if (isStatic)
                    {
                        var cache = GetMemberCache(type);
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var nsNameResolved = type.FullName ?? receiver;
                        AddMemberCompletions(completions, ".", prefixLower, cache.StaticMethods, "method", nsNameResolved, seen);
                        AddMemberCompletions(completions, ".", prefixLower, cache.StaticProperties, "property", nsNameResolved, seen);
                        AddMemberCompletions(completions, ".", prefixLower, cache.StaticFields, "field", nsNameResolved, seen);
                        return completions;
                    }
                    if (instanceType != null)
                    {
                        var cache = GetMemberCache(instanceType);
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var nsNameResolved = instanceType.FullName ?? instanceType.Name;
                        AddMemberCompletions(completions, ".", prefixLower, cache.InstanceMethods, "method", nsNameResolved, seen);
                        AddMemberCompletions(completions, ".", prefixLower, cache.InstanceProperties, "property", nsNameResolved, seen);
                        AddMemberCompletions(completions, ".", prefixLower, cache.InstanceFields, "field", nsNameResolved, seen);
                        return completions;
                    }
                }
            }
            catch { }

            return null;
        }

        private string GetContextSlice(Dictionary<string, object> request)
        {
            var line = request.GetValueOrDefault("line") as string;
            var buffer = request.GetValueOrDefault("buffer") as string
                ?? request.GetValueOrDefault("code") as string
                ?? request.GetValueOrDefault("text") as string;

            int? pos = TryGetInt(request, "pos")
                ?? TryGetInt(request, "cursor")
                ?? TryGetInt(request, "cursor-pos")
                ?? TryGetInt(request, "column");

            if (!string.IsNullOrEmpty(line))
            {
                var slice = line;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, line.Length));
                    slice = line.Substring(0, p);
                }
                return slice;
            }

            if (!string.IsNullOrEmpty(buffer))
            {
                var slice = buffer;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, buffer.Length));
                    slice = buffer.Substring(0, p);
                }
                return slice;
            }

            return "";
        }

        private bool TryParseDotContext(string slice, out string receiver, out string memberPrefix)
        {
            receiver = "";
            memberPrefix = "";

            // Pattern: (. receiver memberPrefix)
            var m = Regex.Match(slice, @"\(\.\s+([^\s\)]+)\s+([^\s\)]*)$");
            if (m.Success)
            {
                receiver = m.Groups[1].Value;
                memberPrefix = m.Groups[2].Value;
                return true;
            }

            // Pattern: (.Member receiver)  -> method-first style
            m = Regex.Match(slice, @"\(\.([^\s\)]+)\s+([^\s\)]*)$");
            if (m.Success)
            {
                memberPrefix = m.Groups[1].Value;
                receiver = m.Groups[2].Value;
                return !string.IsNullOrEmpty(receiver);
            }

            return false;
        }

        private bool TryParseDotContextAny(string text, out string receiver, out string memberPrefix)
        {
            receiver = "";
            memberPrefix = "";
            if (string.IsNullOrEmpty(text)) return false;

            Match bestMatch = null;
            var matches1 = Regex.Matches(text, @"\(\.\s+([^\s\)]+)\s+([^\s\)]*)");
            foreach (Match m in matches1)
            {
                if (!m.Success) continue;
                if (bestMatch == null || m.Index > bestMatch.Index) bestMatch = m;
            }

            var matches2 = Regex.Matches(text, @"\(\.([^\s\)]+)\s+([^\s\)]*)");
            foreach (Match m in matches2)
            {
                if (!m.Success) continue;
                if (bestMatch == null || m.Index > bestMatch.Index) bestMatch = m;
            }

            if (bestMatch == null) return false;

            memberPrefix = bestMatch.Groups[1].Value;
            receiver = bestMatch.Groups[2].Value;

            if (bestMatch.Value.StartsWith("(. ", StringComparison.Ordinal))
            {
                receiver = bestMatch.Groups[1].Value;
                memberPrefix = bestMatch.Groups[2].Value;
            }

            return !string.IsNullOrEmpty(memberPrefix);
        }

        private bool TryGetContextReceiver(Dictionary<string, object> request, out string receiver)
        {
            receiver = "";
            if (!request.TryGetValue("context", out var ctx) || ctx == null) return false;

            if (ctx is List<object> list)
            {
                var parts = new List<string>();
                foreach (var item in list)
                {
                    if (item == null) continue;
                    parts.Add(item.ToString());
                }

                if (parts.Count == 0) return false;

                var prefixIndex = parts.IndexOf("__prefix__");
                if (prefixIndex >= 0 && prefixIndex + 1 < parts.Count)
                {
                    receiver = parts[prefixIndex + 1];
                    return !string.IsNullOrEmpty(receiver);
                }

                if (parts.Count >= 1)
                {
                    receiver = parts[parts.Count - 1];
                    return !string.IsNullOrEmpty(receiver);
                }
            }
            else if (ctx is string s)
            {
                var tokens = Regex.Matches(s, @"[^\s\(\)\[\]]+");
                var parts = new List<string>();
                foreach (Match m in tokens)
                {
                    if (!m.Success) continue;
                    parts.Add(m.Value);
                }
                if (parts.Count == 0) return false;

                var prefixIndex = parts.IndexOf("__prefix__");
                if (prefixIndex >= 0 && prefixIndex + 1 < parts.Count)
                {
                    receiver = parts[prefixIndex + 1];
                    return !string.IsNullOrEmpty(receiver);
                }

                receiver = parts[parts.Count - 1];
                return !string.IsNullOrEmpty(receiver);
            }

            return false;
        }

        private bool TryResolveReceiver(Namespace ns, string receiver, out Type type, out bool isStatic, out Type instanceType)
        {
            type = null;
            instanceType = null;
            isStatic = false;

            if (string.IsNullOrEmpty(receiver)) return false;

            try
            {
                var nsResolve = RT.var("clojure.core", "ns-resolve");
                var resolved = nsResolve.invoke(ns, Symbol.create(receiver));

                if (resolved is Var v)
                {
                    var val = v.deref();
                    if (val is Type vt)
                    {
                        type = vt;
                        isStatic = true;
                        return true;
                    }
                    if (val != null)
                    {
                        instanceType = val.GetType();
                        isStatic = false;
                        return true;
                    }
                }
                else if (resolved is Type rt)
                {
                    type = rt;
                    isStatic = true;
                    return true;
                }
            }
            catch { }

            try
            {
                type = TryResolveType(ns, receiver);
                if (type != null)
                {
                    isStatic = true;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private void AddInstanceCompletions(List<Dictionary<string, object>> completions, string memberPrefixLower, Type type)
        {
            var cache = GetMemberCache(type);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nsName = type.FullName ?? type.Name;

            AddMemberCompletions(completions, "", memberPrefixLower, cache.InstanceMethods, "method", nsName, seen);
            AddMemberCompletions(completions, "", memberPrefixLower, cache.InstanceProperties, "property", nsName, seen);
            AddMemberCompletions(completions, "", memberPrefixLower, cache.InstanceFields, "field", nsName, seen);
        }

        private Dictionary<string, object> TryGetClrMemberInfo(string symbol, Namespace ns, Session session)
        {
            if (string.IsNullOrEmpty(symbol) || !symbol.Contains('/')) return null;
            var parts = symbol.Split('/', 2);
            if (parts.Length != 2) return null;

            var typeAlias = parts[0];
            var member = parts[1];

            var type = TryResolveType(ns, typeAlias);
            if (type == null && session?.CurrentNamespace != null && session.CurrentNamespace != ns)
            {
                type = TryResolveType(session.CurrentNamespace, typeAlias);
            }
            if (type == null) return null;

            var result = new Dictionary<string, object>
            {
                ["ns"] = type.FullName ?? typeAlias,
                ["name"] = member
            };

            var flags = BindingFlags.Public | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            var methodList = new List<MethodInfo>();
            foreach (var m in methods)
            {
                if (m.Name == member) methodList.Add(m);
            }

            if (methodList.Count > 0)
            {
                result["type"] = "method";
                result["arglists"] = BuildArglistsString(methodList);
                return result;
            }

            var prop = type.GetProperty(member, flags);
            if (prop != null)
            {
                result["type"] = "property";
                result["arglists"] = "()";
                result["doc"] = $"Property: {FormatTypeName(prop.PropertyType)}";
                return result;
            }

            var field = type.GetField(member, flags);
            if (field != null)
            {
                result["type"] = "field";
                result["arglists"] = "()";
                result["doc"] = $"Field: {FormatTypeName(field.FieldType)}";
                return result;
            }

            return null;
        }

        private Dictionary<string, object> TryGetClrMemberEldoc(string symbol, Namespace ns, Session session)
        {
            if (string.IsNullOrEmpty(symbol) || !symbol.Contains('/')) return null;
            var parts = symbol.Split('/', 2);
            if (parts.Length != 2) return null;

            var typeAlias = parts[0];
            var member = parts[1];

            var type = TryResolveType(ns, typeAlias);
            if (type == null && session?.CurrentNamespace != null && session.CurrentNamespace != ns)
            {
                type = TryResolveType(session.CurrentNamespace, typeAlias);
            }
            if (type == null) return null;

            var result = new Dictionary<string, object>
            {
                ["ns"] = type.FullName ?? typeAlias,
                ["symbol"] = member
            };

            var flags = BindingFlags.Public | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            var methodList = new List<MethodInfo>();
            foreach (var m in methods)
            {
                if (m.Name == member) methodList.Add(m);
            }

            if (methodList.Count > 0)
            {
                var eldocLists = new List<object>();
                foreach (var m in methodList)
                {
                    var args = new List<string>();
                    var parameters = m.GetParameters();
                    foreach (var p in parameters)
                    {
                        args.Add(FormatParameter(p));
                    }
                    eldocLists.Add(args);
                }
                result["eldoc"] = eldocLists;
                result["type"] = "method";
                return result;
            }

            var prop = type.GetProperty(member, flags);
            if (prop != null)
            {
                result["eldoc"] = new List<object> { new List<string>() };
                result["type"] = "property";
                result["docstring"] = $"Property: {FormatTypeName(prop.PropertyType)}";
                return result;
            }

            var field = type.GetField(member, flags);
            if (field != null)
            {
                result["eldoc"] = new List<object> { new List<string>() };
                result["type"] = "field";
                result["docstring"] = $"Field: {FormatTypeName(field.FieldType)}";
                return result;
            }

            return null;
        }

        private string BuildArglistsString(List<MethodInfo> methods)
        {
            var sigs = new List<string>();
            foreach (var m in methods)
            {
                var parameters = m.GetParameters();
                var parts = new List<string>();
                foreach (var p in parameters)
                {
                    parts.Add(FormatParameter(p));
                }
                sigs.Add($"[{string.Join(" ", parts)}]");
            }
            return $"({string.Join(" ", sigs)})";
        }

        private string FormatParameter(ParameterInfo p)
        {
            var type = p.ParameterType;
            var prefix = "";
            if (type.IsByRef)
            {
                type = type.GetElementType();
                prefix = p.IsOut ? "out " : "ref ";
            }
            var typeName = FormatTypeName(type);
            if (string.IsNullOrEmpty(p.Name)) return $"{prefix}{typeName}".Trim();
            return $"{prefix}{typeName} {p.Name}".Trim();
        }

        private string FormatTypeName(Type t)
        {
            if (t == null) return "";
            if (t.IsArray)
            {
                return $"{FormatTypeName(t.GetElementType())}[]";
            }
            if (t.IsGenericType)
            {
                var name = t.Name;
                var tick = name.IndexOf('`');
                if (tick > 0) name = name.Substring(0, tick);
                var args = t.GetGenericArguments();
                var argNames = new List<string>();
                foreach (var a in args) argNames.Add(FormatTypeName(a));
                return $"{name}<{string.Join(",", argNames)}>";
            }
            return t.Name;
        }

        private string ExtractPrefixFromRequest(Dictionary<string, object> request)
        {
            // Try to derive prefix from line/buffer + cursor position
            var line = request.GetValueOrDefault("line") as string;
            var buffer = request.GetValueOrDefault("buffer") as string
                ?? request.GetValueOrDefault("code") as string
                ?? request.GetValueOrDefault("text") as string;

            int? pos = TryGetInt(request, "pos")
                ?? TryGetInt(request, "cursor")
                ?? TryGetInt(request, "cursor-pos")
                ?? TryGetInt(request, "column");

            if (!string.IsNullOrEmpty(line))
            {
                var slice = line;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, line.Length));
                    slice = line.Substring(0, p);
                }
                return ExtractToken(slice);
            }

            if (!string.IsNullOrEmpty(buffer))
            {
                var slice = buffer;
                if (pos.HasValue)
                {
                    var p = Math.Max(0, Math.Min(pos.Value, buffer.Length));
                    slice = buffer.Substring(0, p);
                }
                return ExtractToken(slice);
            }

            return "";
        }

        private string FormatCompleteDebug(Dictionary<string, object> request)
        {
            var fields = new List<string>();
            AddDebugField(fields, request, "symbol");
            AddDebugField(fields, request, "prefix");
            AddDebugField(fields, request, "sym");
            AddDebugField(fields, request, "ns");
            AddDebugField(fields, request, "pos");
            AddDebugField(fields, request, "cursor");
            AddDebugField(fields, request, "cursor-pos");
            AddDebugField(fields, request, "column");
            AddDebugTextField(fields, request, "line");
            AddDebugTextField(fields, request, "buffer");
            AddDebugTextField(fields, request, "code");
            AddDebugTextField(fields, request, "text");
            AddDebugContext(fields, request, "context");
            return string.Join(", ", fields);
        }

        private void AddDebugField(List<string> fields, Dictionary<string, object> request, string key)
        {
            if (!request.TryGetValue(key, out var val) || val == null) return;
            fields.Add($"{key}={val}");
        }

        private void AddDebugTextField(List<string> fields, Dictionary<string, object> request, string key)
        {
            if (!request.TryGetValue(key, out var val) || val == null) return;
            var text = val.ToString() ?? "";
            if (text.Length > 80)
            {
                var start = Math.Max(0, text.Length - 80);
                var tail = text.Substring(start);
                fields.Add($"{key}=\"...{tail}\"(len={text.Length})");
            }
            else
            {
                fields.Add($"{key}=\"{text}\"(len={text.Length})");
            }
        }

        private void AddDebugContext(List<string> fields, Dictionary<string, object> request, string key)
        {
            if (!request.TryGetValue(key, out var val) || val == null) return;
            if (val is List<object> list)
            {
                var parts = new List<string>();
                foreach (var item in list)
                {
                    parts.Add(item?.ToString() ?? "");
                }
                fields.Add($"{key}=[{string.Join(" ", parts)}]");
                return;
            }
            fields.Add($"{key}={val}");
        }

        private Namespace ResolveNamespace(string nsName, Session session)
        {
            Namespace ns = null;
            if (!string.IsNullOrEmpty(nsName))
            {
                try
                {
                    var findNs = RT.var("clojure.core", "find-ns");
                    ns = findNs.invoke(Symbol.create(nsName)) as Namespace;
                }
                catch { }

                if (ns != null)
                {
                    if (nsName == "clojure.core" && session?.CurrentNamespace != null)
                    {
                        var sessionNsName = session.CurrentNamespace?.Name?.Name;
                        if (!string.IsNullOrEmpty(sessionNsName) && sessionNsName != "clojure.core")
                        {
                            return session.CurrentNamespace;
                        }
                    }
                    return ns;
                }
            }

            return session?.CurrentNamespace ?? RT.CurrentNSVar.deref() as Namespace;
        }

        private int? TryGetInt(Dictionary<string, object> request, string key)
        {
            if (!request.TryGetValue(key, out var val) || val == null) return null;
            try
            {
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is string s && int.TryParse(s, out var si)) return si;
            }
            catch { }
            return null;
        }

        private string ExtractToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int i = s.Length - 1;
            while (i >= 0 && IsTokenChar(s[i])) i--;
            return s.Substring(i + 1);
        }

        private bool IsTokenChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '+' || c == '*'
                || c == '?' || c == '!' || c == '$' || c == '<' || c == '>' || c == '='
                || c == '.' || c == ':' || c == '/' || c == '\\' || c == '\'';
        }

        private static bool IsEnvTrue(string name)
        {
            var val = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(val)) return false;
            return val.Equals("1") || val.Equals("true", StringComparison.OrdinalIgnoreCase) || val.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private string GetFullNamespaceName(string shorthand)
        {
            // Common namespace shorthands used in Clojure
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["str"] = "clojure.string",
                ["string"] = "clojure.string",
                ["io"] = "clojure.java.io",
                ["set"] = "clojure.set",
                ["walk"] = "clojure.walk",
                ["zip"] = "clojure.zip",
                ["edn"] = "clojure.edn",
                ["pprint"] = "clojure.pprint",
                ["inspect"] = "clojure.inspector",
                ["repl"] = "clojure.repl",
                ["template"] = "clojure.template",
                ["test"] = "clojure.test",
                ["data"] = "clojure.data",
                ["xml"] = "clojure.data.xml"
            };

            return mappings.GetValueOrDefault(shorthand);
        }

        private void HandleInfo(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, Session session)
        {
            var id = GetId(request);
            // CIDER uses 'sym' not 'symbol' for info
            var symbol = request.GetValueOrDefault("sym") as string 
                ?? request.GetValueOrDefault("symbol") as string 
                ?? "";
            var nsName = request.GetValueOrDefault("ns") as string;

            Console.WriteLine($"Info request: sym='{symbol}', ns='{nsName}'");

            try
            {
                var info = GetSymbolInfo(symbol, nsName, session);
                info["id"] = id;
                info["session"] = sessionId;
                info["status"] = new List<string> { "done" };

                SendResponse(stream, info, useLengthPrefix);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Info error: {e.Message}");
                SendResponse(stream, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["status"] = new List<string> { "done" }
                }, useLengthPrefix);
            }
        }

        private Dictionary<string, object> GetSymbolInfo(string symbol, string nsName, Session session)
        {
            var result = new Dictionary<string, object>();

            // Get the namespace to search in
            var ns = ResolveNamespace(nsName, session);

            if (ns == null) return result;

            // CLR static member info (Type/Member)
            var clrInfo = TryGetClrMemberInfo(symbol, ns, session);
            if (clrInfo != null && clrInfo.Count > 0)
            {
                return clrInfo;
            }

            // Handle namespace-qualified symbols (e.g., clojure.string/join)
            Var targetVar = null;
            if (symbol.Contains('/'))
            {
                var parts = symbol.Split('/', 2);
                var nsPart = parts[0];
                var namePart = parts[1];

                // Try to resolve namespace
                Namespace targetNs = null;

                // 1. Try as full namespace name
                try
                {
                    var findNs = RT.var("clojure.core", "find-ns");
                    targetNs = findNs.invoke(Symbol.create(nsPart)) as Namespace;
                }
                catch { }

                // 2. Try as shorthand
                if (targetNs == null)
                {
                    var fullNs = GetFullNamespaceName(nsPart);
                    if (!string.IsNullOrEmpty(fullNs))
                    {
                        try
                        {
                            var require = RT.var("clojure.core", "require");
                            require.invoke(Symbol.create(fullNs));
                        }
                        catch { }

                        try
                        {
                            var findNs = RT.var("clojure.core", "find-ns");
                            targetNs = findNs.invoke(Symbol.create(fullNs)) as Namespace;
                        }
                        catch { }
                    }
                }

                if (targetNs != null)
                {
                    try
                    {
                        var nsInterns = RT.var("clojure.core", "ns-interns");
                        var interns = nsInterns.invoke(targetNs) as IPersistentMap;
                        if (interns != null)
                        {
                            targetVar = interns.valAt(Symbol.create(namePart)) as Var;
                        }
                    }
                    catch { }
                }
            }
            else
            {
                // Look up in current namespace using ns-resolve
                try
                {
                    var nsResolve = RT.var("clojure.core", "ns-resolve");
                    targetVar = nsResolve.invoke(ns, Symbol.create(symbol)) as Var;
                }
                catch { }
            }

            if (targetVar != null)
            {
                result["ns"] = targetVar.Namespace?.Name?.Name ?? "";
                result["name"] = targetVar.Symbol?.Name ?? symbol;
                result["arglists"] = "";
                result["doc"] = "";

                try
                {
                    var meta = targetVar.meta() as IPersistentMap;
                    if (meta != null)
                    {
                        var docVal = meta.valAt(Keyword.intern("doc"));
                        if (docVal != null) result["doc"] = docVal.ToString();

                        var arglists = meta.valAt(Keyword.intern("arglists"));
                        if (arglists != null) result["arglists"] = arglists.ToString();

                        var line = meta.valAt(Keyword.intern("line"));
                        if (line != null) result["line"] = line.ToString();

                        var file = meta.valAt(Keyword.intern("file"));
                        if (file != null) result["file"] = file.ToString();
                    }
                }
                catch { }
            }

            return result;
        }

        private void HandleEldoc(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, Session session)
        {
            var id = GetId(request);
            // CIDER uses 'sym' not 'symbol' for eldoc
            var symbol = request.GetValueOrDefault("sym") as string 
                ?? request.GetValueOrDefault("symbol") as string 
                ?? "";
            var nsName = request.GetValueOrDefault("ns") as string;

            if (debugEldoc)
            {
                Console.WriteLine($"Eldoc request fields: {FormatCompleteDebug(request)}");
            }
            Console.WriteLine($"Eldoc request: sym='{symbol}', ns='{nsName}'");

            try
            {
                var eldocInfo = GetEldocInfo(symbol, nsName, session);
                
                // Check if we found valid eldoc info
                var eldocList = eldocInfo.GetValueOrDefault("eldoc") as List<object>;
                bool hasEldoc = false;
                if (eldocList != null && eldocList.Count > 0)
                {
                    // Check if first element is a non-empty list (could be List<object> or List<string>)
                    var first = eldocList[0];
                    if (first is List<object> objList && objList.Count > 0)
                        hasEldoc = true;
                    else if (first is List<string> strList && strList.Count > 0)
                        hasEldoc = true;
                }
                
                if (!hasEldoc)
                {
                    var dotEldoc = TryGetDotEldoc(request, symbol, nsName, session);
                    if (dotEldoc != null && dotEldoc.GetValueOrDefault("eldoc") is List<object> dotList && dotList.Count > 0)
                    {
                        dotEldoc["id"] = id;
                        dotEldoc["session"] = sessionId;
                        dotEldoc["status"] = new List<string> { "done" };
                        SendResponse(stream, dotEldoc, useLengthPrefix);
                    }
                    else
                    {
                        // Return no-eldoc status when no info found (CIDER expects this)
                        SendResponse(stream, new Dictionary<string, object>
                        {
                            ["id"] = id,
                            ["session"] = sessionId,
                            ["status"] = new List<string> { "no-eldoc" }
                        }, useLengthPrefix);
                    }
                }
                else
                {
                    eldocInfo["id"] = id;
                    eldocInfo["session"] = sessionId;
                    eldocInfo["status"] = new List<string> { "done" };
                    SendResponse(stream, eldocInfo, useLengthPrefix);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Eldoc error: {e.Message}");
                // Return no-eldoc on error
                SendResponse(stream, new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["session"] = sessionId,
                    ["status"] = new List<string> { "no-eldoc" }
                }, useLengthPrefix);
            }
        }

        private Dictionary<string, object> GetEldocInfo(string symbol, string nsName, Session session)
        {
            var result = new Dictionary<string, object>();
            
            // Get the namespace to search in
            var ns = ResolveNamespace(nsName, session);
            
            if (ns == null || string.IsNullOrEmpty(symbol)) return result;

            // CLR static member eldoc (Type/Member)
            var clrEldoc = TryGetClrMemberEldoc(symbol, ns, session);
            if (clrEldoc != null && clrEldoc.Count > 0)
            {
                return clrEldoc;
            }

            // Resolve the symbol to get the var
            Var targetVar = null;
            
            // Handle namespace-qualified symbols (e.g., clojure.string/join)
            if (symbol.Contains('/'))
            {
                var parts = symbol.Split('/', 2);
                var nsPart = parts[0];
                var namePart = parts[1];
                
                Namespace targetNs = null;
                
                // 1. Try as full namespace name
                try
                {
                    var findNs = RT.var("clojure.core", "find-ns");
                    targetNs = findNs.invoke(Symbol.create(nsPart)) as Namespace;
                }
                catch { }
                
                // 2. Try as shorthand
                if (targetNs == null)
                {
                    var fullNs = GetFullNamespaceName(nsPart);
                    if (!string.IsNullOrEmpty(fullNs))
                    {
                        try
                        {
                            var require = RT.var("clojure.core", "require");
                            require.invoke(Symbol.create(fullNs));
                        }
                        catch { }
                        
                        try
                        {
                            var findNs = RT.var("clojure.core", "find-ns");
                            targetNs = findNs.invoke(Symbol.create(fullNs)) as Namespace;
                        }
                        catch { }
                    }
                }
                
                if (targetNs != null)
                {
                    try
                    {
                        var nsInterns = RT.var("clojure.core", "ns-interns");
                        var interns = nsInterns.invoke(targetNs) as IPersistentMap;
                        if (interns != null)
                        {
                            targetVar = interns.valAt(Symbol.create(namePart)) as Var;
                        }
                    }
                    catch { }
                }
            }
            else
            {
                // Look up in current namespace
                try
                {
                    var nsResolve = RT.var("clojure.core", "ns-resolve");
                    targetVar = nsResolve.invoke(ns, Symbol.create(symbol)) as Var;
                }
                catch { }
            }
            
            if (targetVar != null)
            {
                // Update ns to the actual namespace of the var
                try
                {
                    var varNs = targetVar.Namespace?.Name?.Name;
                    if (!string.IsNullOrEmpty(varNs))
                        result["ns"] = varNs;
                }
                catch { }
                
                // Update symbol to the actual var name
                try
                {
                    var varName = targetVar.Symbol?.Name;
                    if (!string.IsNullOrEmpty(varName))
                        result["symbol"] = varName;
                }
                catch { }
                
                try
                {
                    var meta = targetVar.meta() as IPersistentMap;
                    if (meta != null)
                    {
                        var arglists = meta.valAt(Keyword.intern("arglists"));
                        if (arglists != null)
                        {
                            // arglists is a PersistentVector of PersistentVectors
                            // Convert to list of lists for eldoc
                            var eldocLists = new List<object>();
                            
                            if (arglists is IPersistentVector outerVec)
                            {
                                for (int i = 0; i < outerVec.count(); i++)
                                {
                                    var arglist = outerVec.nth(i);
                                    var args = new List<string>();
                                    
                                    if (arglist is IPersistentVector innerVec)
                                    {
                                        for (int j = 0; j < innerVec.count(); j++)
                                        {
                                            args.Add(innerVec.nth(j)?.ToString() ?? "");
                                        }
                                    }
                                    else if (arglist is ISeq seq)
                                    {
                                        var s = seq;
                                        while (s != null && s.count() > 0)
                                        {
                                            args.Add(s.first()?.ToString() ?? "");
                                            s = s.next();
                                        }
                                    }
                                    else
                                    {
                                        args.Add(arglist?.ToString() ?? "");
                                    }
                                    
                                    eldocLists.Add(args);
                                }
                            }
                            else if (arglists is ISeq seq)
                            {
                                var s = seq;
                                while (s != null && s.count() > 0)
                                {
                                    var arglist = s.first();
                                    var args = new List<string>();
                                    if (arglist is IPersistentVector innerVec)
                                    {
                                        for (int j = 0; j < innerVec.count(); j++)
                                        {
                                            args.Add(innerVec.nth(j)?.ToString() ?? "");
                                        }
                                    }
                                    else if (arglist is ISeq innerSeq)
                                    {
                                        var innerS = innerSeq;
                                        while (innerS != null && innerS.count() > 0)
                                        {
                                            args.Add(innerS.first()?.ToString() ?? "");
                                            innerS = innerS.next();
                                        }
                                    }
                                    else
                                    {
                                        args.Add(arglist?.ToString() ?? "");
                                    }
                                    eldocLists.Add(args);
                                    s = s.next();
                                }
                            }
                            
                            result["eldoc"] = eldocLists;
                            result["type"] = "function";
                            
                            // Also add docstring
                            var docVal = meta.valAt(Keyword.intern("doc"));
                            if (docVal != null) result["docstring"] = docVal.ToString();
                        }
                        
                        // Check if it's a macro
                        var macroVal = meta.valAt(Keyword.intern("macro"));
                        if (macroVal != null && macroVal.ToString() == "true")
                        {
                            result["type"] = "macro";
                        }
                    }
                }
                catch { }
            }
            
            return result;
        }

        private string CreateSession()
        {
            var id = Guid.NewGuid().ToString();
            sessions[id] = new Session { Id = id };
            return id;
        }
    }

    class Session
    {
        public string Id { get; set; }
        public Namespace CurrentNamespace { get; set; }
    }

}

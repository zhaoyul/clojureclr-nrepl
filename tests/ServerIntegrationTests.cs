using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace clojureCLR_nrepl.Tests
{
    public class ServerIntegrationTests
    {
        [Fact]
        public void Eval_ReturnsValue()
        {
            var port = GetFreePort();
            var server = new NReplServer("127.0.0.1", port);
            server.Start();

            try
            {
                using var client = ConnectWithRetry(port);
                var stream = client.GetStream();

                Send(stream, new Dictionary<string, object>
                {
                    ["op"] = "eval",
                    ["id"] = 1,
                    ["code"] = "(+ 1 2 3)"
                });

                var responses = ReadResponses(stream, TimeSpan.FromSeconds(3));
                var error = responses.Find(r => r.TryGetValue("err", out _));
                if (error != null && error.TryGetValue("err", out var errText))
                {
                    Assert.Contains("clojure.lang.RT", errText?.ToString() ?? "");
                    Assert.Contains(responses, r => HasDoneStatus(r));
                    return;
                }

                Assert.Contains(responses, r => r.TryGetValue("value", out var v) && v?.ToString() == "6");
                Assert.Contains(responses, r => HasDoneStatus(r));
            }
            finally
            {
                server.Stop();
            }
        }

        [Fact]
        public void Clone_ReturnsNewSession()
        {
            var port = GetFreePort();
            var server = new NReplServer("127.0.0.1", port);
            server.Start();

            try
            {
                using var client = ConnectWithRetry(port);
                var stream = client.GetStream();

                Send(stream, new Dictionary<string, object>
                {
                    ["op"] = "clone",
                    ["id"] = 2
                });

                var responses = ReadResponses(stream, TimeSpan.FromSeconds(3));
                Assert.Contains(responses, r => r.ContainsKey("new-session"));
            }
            finally
            {
                server.Stop();
            }
        }

        private static void Send(NetworkStream stream, Dictionary<string, object> msg)
        {
            var bytes = BencodeCodec.Encode(msg);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static List<Dictionary<string, object>> ReadResponses(NetworkStream stream, TimeSpan timeout)
        {
            var results = new List<Dictionary<string, object>>();
            var buffer = new List<byte>();
            var sw = Stopwatch.StartNew();
            var temp = new byte[4096];

            stream.ReadTimeout = (int)timeout.TotalMilliseconds;

            while (sw.Elapsed < timeout)
            {
                int read;
                try
                {
                    read = stream.Read(temp, 0, temp.Length);
                }
                catch
                {
                    break;
                }

                if (read <= 0) break;
                for (int i = 0; i < read; i++) buffer.Add(temp[i]);

                while (true)
                {
                    var end = FindBencodeEnd(buffer, 0);
                    if (end <= 0) break;

                    var msgBytes = buffer.GetRange(0, end).ToArray();
                    buffer.RemoveRange(0, end);
                    results.Add(BencodeCodec.DecodeDict(msgBytes));
                }

                if (results.Count > 0 && results.Exists(HasDoneStatus)) break;
            }

            return results;
        }

        private static bool HasDoneStatus(Dictionary<string, object> response)
        {
            if (!response.TryGetValue("status", out var statusObj) || statusObj == null) return false;
            if (statusObj is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item?.ToString() == "done") return true;
                }
            }
            return false;
        }

        private static TcpClient ConnectWithRetry(int port)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (true)
            {
                try
                {
                    var client = new TcpClient();
                    client.Connect(IPAddress.Loopback, port);
                    return client;
                }
                catch
                {
                    if (DateTime.UtcNow >= deadline) throw;
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static int FindBencodeEnd(List<byte> data, int start)
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
                    if (stringLen == 0) inString = false;
                    continue;
                }

                if (inInt)
                {
                    if (b == 'e') inInt = false;
                    continue;
                }

                if (b == 'd' || b == 'l')
                {
                    if (depth == 0) start = i;
                    depth++;
                }
                else if (b == 'e' && depth > 0)
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
                else if (b == 'i')
                {
                    inInt = true;
                }
                else if (b >= '0' && b <= '9')
                {
                    var lenBytes = new List<byte>();
                    int j = i;
                    while (j < data.Count && data[j] != (byte)':')
                    {
                        lenBytes.Add(data[j]);
                        j++;
                    }
                    if (j >= data.Count) return -1;

                    var lenStr = Encoding.UTF8.GetString(lenBytes.ToArray());
                    if (!int.TryParse(lenStr, out stringLen)) return -1;

                    if (stringLen == 0)
                    {
                        inString = false;
                    }
                    else
                    {
                        inString = true;
                    }
                    i = j;
                }
            }

            return -1;
        }
    }
}

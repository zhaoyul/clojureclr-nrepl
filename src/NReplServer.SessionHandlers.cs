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

    }
}
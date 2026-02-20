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
        private void HandleEval(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, NReplSession session)
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

        private string EvaluateClojure(string code, NReplSession session)
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

    }
}
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
        private void HandleEldoc(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, NReplSession session)
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

        private Dictionary<string, object> GetEldocInfo(string symbol, string nsName, NReplSession session)
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

    }
}
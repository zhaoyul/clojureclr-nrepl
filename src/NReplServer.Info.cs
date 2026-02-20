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
        private void HandleInfo(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, NReplSession session)
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

        private Dictionary<string, object> GetSymbolInfo(string symbol, string nsName, NReplSession session)
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

        private Dictionary<string, object> TryGetClrMemberInfo(string symbol, Namespace ns, NReplSession session)
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

    }
}
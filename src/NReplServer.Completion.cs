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
        private void HandleComplete(NetworkStream stream, Dictionary<string, object> request, string sessionId, bool useLengthPrefix, NReplSession session)
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

        private List<Dictionary<string, object>> GetCompletions(string prefix, string nsName, NReplSession session)
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

    }
}
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
        private List<Dictionary<string, object>> TryGetDotCompletions(Dictionary<string, object> request, string nsName, NReplSession session)
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

        private List<Dictionary<string, object>> TryGetDotCompletionsFromPrefix(string prefix, Dictionary<string, object> request, string nsName, NReplSession session)
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
                        var cache = GetCompletionCache(type);
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var nsNameResolved = type.FullName ?? receiver;
                        AddMemberCompletions(completions, ".", prefixLower, cache.StaticMethods, "method", nsNameResolved, seen);
                        AddMemberCompletions(completions, ".", prefixLower, cache.StaticProperties, "property", nsNameResolved, seen);
                        AddMemberCompletions(completions, ".", prefixLower, cache.StaticFields, "field", nsNameResolved, seen);
                        return completions;
                    }
                    if (instanceType != null)
                    {
                        var cache = GetCompletionCache(instanceType);
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

        private void AddInstanceCompletions(List<Dictionary<string, object>> completions, string memberPrefixLower, Type type)
        {
            var cache = GetCompletionCache(type);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nsName = type.FullName ?? type.Name;

            AddMemberCompletions(completions, "", memberPrefixLower, cache.InstanceMethods, "method", nsName, seen);
            AddMemberCompletions(completions, "", memberPrefixLower, cache.InstanceProperties, "property", nsName, seen);
            AddMemberCompletions(completions, "", memberPrefixLower, cache.InstanceFields, "field", nsName, seen);
        }

    }
}
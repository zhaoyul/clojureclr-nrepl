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
        private Dictionary<string, object> TryGetDotEldoc(Dictionary<string, object> request, string symbol, string nsName, NReplSession session)
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

    }
}
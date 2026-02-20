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

        private Namespace ResolveNamespace(string nsName, NReplSession session)
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

    }
}
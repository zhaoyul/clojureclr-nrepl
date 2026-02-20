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
        private Dictionary<string, object> TryGetClrMemberEldoc(string symbol, Namespace ns, NReplSession session)
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

    }
}
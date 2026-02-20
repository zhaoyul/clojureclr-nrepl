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
        private CompletionCache GetCompletionCache(Type type)
        {
            return CompletionCacheByType.GetOrAdd(type, BuildCompletionCache);
        }

        private CompletionCache BuildCompletionCache(Type type)
        {
            var cache = new CompletionCache
            {
                StaticMethods = GetMethodNames(type, BindingFlags.Public | BindingFlags.Static),
                StaticProperties = GetPropertyNames(type, BindingFlags.Public | BindingFlags.Static),
                StaticFields = GetFieldNames(type, BindingFlags.Public | BindingFlags.Static),
                InstanceMethods = GetMethodNames(type, BindingFlags.Public | BindingFlags.Instance),
                InstanceProperties = GetPropertyNames(type, BindingFlags.Public | BindingFlags.Instance),
                InstanceFields = GetFieldNames(type, BindingFlags.Public | BindingFlags.Instance)
            };

            return cache;
        }

        private string[] GetMethodNames(Type type, BindingFlags flags)
        {
            try
            {
                var methods = type.GetMethods(flags);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var method in methods)
                {
                    if (method.IsSpecialName) continue; // skip get_/set_/op_*
                    names.Add(method.Name);
                }
                return new List<string>(names).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private string[] GetPropertyNames(Type type, BindingFlags flags)
        {
            try
            {
                var properties = type.GetProperties(flags);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in properties)
                {
                    names.Add(prop.Name);
                }
                return new List<string>(names).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private string[] GetFieldNames(Type type, BindingFlags flags)
        {
            try
            {
                var fields = type.GetFields(flags);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in fields)
                {
                    names.Add(field.Name);
                }
                return new List<string>(names).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private void AddMemberCompletions(List<Dictionary<string, object>> completions, string candidatePrefix, string memberPrefixLower, string[] members, string memberType, string nsName, HashSet<string> seen)
        {
            if (members == null || members.Length == 0) return;
            foreach (var name in members)
            {
                if (!name.ToLowerInvariant().StartsWith(memberPrefixLower)) continue;
                if (!seen.Add(name)) continue;

                completions.Add(new Dictionary<string, object>
                {
                    ["candidate"] = $"{candidatePrefix}{name}",
                    ["type"] = memberType,
                    ["ns"] = nsName,
                    ["doc"] = ""
                });
            }
        }

        private void AddTypeCompletions(List<Dictionary<string, object>> completions, string typeAlias, string memberPrefixLower, Type type)
        {
            var cache = GetCompletionCache(type);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nsName = type.FullName ?? typeAlias;
            var prefix = $"{typeAlias}/";

            AddMemberCompletions(completions, prefix, memberPrefixLower, cache.StaticMethods, "method", nsName, seen);
            AddMemberCompletions(completions, prefix, memberPrefixLower, cache.StaticProperties, "property", nsName, seen);
            AddMemberCompletions(completions, prefix, memberPrefixLower, cache.StaticFields, "field", nsName, seen);
        }

    }
}
#pragma warning disable CA2255

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;
using clojure.lang;

namespace clojureCLR_nrepl
{
    internal static class RTDefaultImportsPatch
    {
        private static int _patched;

        [ModuleInitializer]
        internal static void Init()
        {
            TryPatch();
        }

        internal static void TryPatch()
        {
            if (Interlocked.Exchange(ref _patched, 1) != 0) return;

            try
            {
                var rtType = Type.GetType("clojure.lang.RT, Clojure");
                if (rtType == null) return;

                var target = rtType.GetMethod(
                    "CreateDefaultImportDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (target == null) return;

                var prefix = typeof(RTDefaultImportsPatch).GetMethod(
                    nameof(CreateDefaultImportDictionaryPrefix),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (prefix == null) return;

                var harmony = new Harmony("clojureclr.nrepl.rt-default-imports");
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            }
            catch
            {
                // Best-effort patch: if anything fails, leave default behavior unchanged.
            }
        }

        private static bool CreateDefaultImportDictionaryPrefix(ref Dictionary<Symbol, Type> __result)
        {
            var dict = new Dictionary<Symbol, Type>();

            foreach (var t in GetAllTypesInNamespace("System"))
            {
                var sym = Symbol.intern(t.Name);
                if (!dict.ContainsKey(sym))
                {
                    dict[sym] = t;
                }
            }

            dict[Symbol.intern("StringBuilder")] = typeof(StringBuilder);
            dict[Symbol.intern("BigInteger")] = typeof(clojure.lang.BigInteger);
            dict[Symbol.intern("BigDecimal")] = typeof(clojure.lang.BigDecimal);

            __result = dict;
            return false;
        }

        private static IEnumerable<Type> GetAllTypesInNamespace(string nspace)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if ((t.IsClass || t.IsInterface || t.IsValueType)
                        && t.Namespace == nspace
                        && (t.IsPublic || t.IsNestedPublic)
                        && !t.IsGenericTypeDefinition
                        && !t.Name.StartsWith("_")
                        && !t.Name.StartsWith("<"))
                    {
                        yield return t;
                    }
                }
            }
        }
    }
}

#pragma warning restore CA2255

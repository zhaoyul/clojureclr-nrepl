#pragma warning disable CA2255

using System;
using System.Collections.Generic;
using System.IO;
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

                var tryLoadTarget = rtType.GetMethod(
                    "TryLoadFromEmbeddedResource",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var tryLoadPrefix = typeof(RTDefaultImportsPatch).GetMethod(
                    nameof(TryLoadFromEmbeddedResourcePrefix),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (tryLoadTarget != null && tryLoadPrefix != null)
                {
                    harmony.Patch(tryLoadTarget, prefix: new HarmonyMethod(tryLoadPrefix));
                }
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
            dict[Symbol.intern("BigInteger")] = typeof(global::System.Numerics.BigInteger);
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

        private static bool TryLoadFromEmbeddedResourcePrefix(string relativePath, string assemblyname, ref bool __result)
        {
            if (TryLoadAssemblyResource(relativePath, assemblyname))
            {
                __result = true;
                return false;
            }

            var dotName = relativePath.Replace("/", ".") + ".cljr";
            if (TryLoadSourceResource(relativePath, dotName))
            {
                __result = true;
                return false;
            }

            dotName = relativePath.Replace("/", ".") + ".cljc";
            if (TryLoadSourceResource(relativePath, dotName))
            {
                __result = true;
                return false;
            }

            dotName = relativePath.Replace("/", ".") + ".clj";
            if (TryLoadSourceResource(relativePath, dotName))
            {
                __result = true;
                return false;
            }

            __result = false;
            return false;
        }

        private static bool TryLoadAssemblyResource(string relativePath, string assemblyName)
        {
            var stream = GetEmbeddedResourceStreamSafe(assemblyName, out _);
            if (stream == null) return false;

            using (stream)
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                var loadAssembly = typeof(Compiler).GetMethod(
                    "LoadAssembly",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(byte[]), typeof(string) },
                    null);
                if (loadAssembly == null) return false;

                Var.pushThreadBindings(RT.map(
                    RT.CurrentNSVar, RT.CurrentNSVar.deref(),
                    RT.WarnOnReflectionVar, RT.WarnOnReflectionVar.deref(),
                    RT.UncheckedMathVar, RT.UncheckedMathVar.deref()));
                try
                {
                    loadAssembly.Invoke(null, new object[] { bytes, relativePath });
                }
                finally
                {
                    Var.popThreadBindings();
                }
            }

            return true;
        }

        private static bool TryLoadSourceResource(string relativePath, string resourceName)
        {
            var stream = GetEmbeddedResourceStreamSafe(resourceName, out var containingAssembly);
            if (stream == null) return false;

            using (stream)
            using (var reader = new StreamReader(stream))
            {
                Var.pushThreadBindings(RT.map(
                    RT.CurrentNSVar, RT.CurrentNSVar.deref(),
                    RT.WarnOnReflectionVar, RT.WarnOnReflectionVar.deref(),
                    RT.UncheckedMathVar, RT.UncheckedMathVar.deref()));
                try
                {
                    var compileFilesField = typeof(Compiler).GetField(
                        "CompileFilesVar",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var compileFilesVar = compileFilesField?.GetValue(null) as Var;
                    var compileFiles = compileFilesVar?.deref();
                    var compileFlag = compileFiles != null && RT.booleanCast(compileFiles);
                    var assemblyName = containingAssembly?.FullName ?? typeof(RT).Assembly.FullName ?? "unknown";

                    if (compileFlag)
                    {
                        var compile = typeof(RT).GetMethod(
                            "Compile",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        if (compile == null) return false;
                        compile.Invoke(null, new object[] { assemblyName, resourceName, reader, relativePath });
                    }
                    else
                    {
                        var loadScript = typeof(RT).GetMethod(
                            "LoadScript",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                            null,
                            new[] { typeof(string), typeof(string), typeof(TextReader), typeof(string) },
                            null);
                        if (loadScript == null) return false;
                        loadScript.Invoke(null, new object[] { assemblyName, resourceName, reader, relativePath });
                    }
                }
                finally
                {
                    Var.popThreadBindings();
                }
            }

            return true;
        }

        private static Stream GetEmbeddedResourceStreamSafe(string resourceName, out System.Reflection.Assembly containingAssembly)
        {
            containingAssembly = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                try
                {
                    var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        containingAssembly = assembly;
                        return stream;
                    }
                }
                catch
                {
                    // ignore and continue
                }
            }

            return null;
        }
    }
}

#pragma warning restore CA2255

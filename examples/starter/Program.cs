using System;
using System.IO;
using System.Reflection;
using clojure.lang;
using clojureCLR_nrepl;
using Newtonsoft.Json.Linq;

namespace ClojureClrStarter
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var cljPath = ResolveClojureEntryPath();
            if (string.IsNullOrWhiteSpace(cljPath))
            {
                throw new InvalidOperationException(
                    "Cannot locate app core.clj. " +
                    "Set CORECLR_STUDIO_CLJ_PATH to an explicit path or keep src/app/core.clj in your project.");
            }

            PreloadClojureAssemblies();
            BootstrapClojureLoaders(AppContext.BaseDirectory);

            NReplServer? server = null;
            var nreplEnabled = IsEnabled("NREPL_ENABLE");
            if (IsEnabled("NREPL_ENABLE"))
            {
                var host = Environment.GetEnvironmentVariable("NREPL_HOST") ?? "127.0.0.1";
                var port = ParseInt(Environment.GetEnvironmentVariable("NREPL_PORT"), 1667);
                server = new NReplServer(host, port);
                server.Start();
                Console.WriteLine($"nREPL server started on {host}:{port}");
            }

            try
            {
                // Bootstrap RT/Core before touching Compiler directly.
                RT.var("clojure.core", "+");

                Console.WriteLine("Loading Clojure file:");
                Console.WriteLine($"  {cljPath}");
                Compiler.loadFile(cljPath);

                var mainVar = RT.var("app.core", "-main");
                if (mainVar == null)
                {
                    throw new InvalidOperationException("Cannot resolve app.core/-main");
                }

                // Use Clojure -main as the entry point.
                mainVar.applyTo(RT.seq(args));

                // Optional C# package demo (kept separate from the Clojure entry).
                var json = JObject.Parse("{\"ok\":true,\"n\":42}");
                Console.WriteLine($"json ok => {json["ok"]}, n => {json["n"]}");

                if (nreplEnabled)
                {
                    Console.WriteLine("Press Enter to stop...");
                    Console.ReadLine();
                }
            }
            finally
            {
                server?.Stop();
            }
        }

        private static bool IsEnabled(string key)
        {
            var raw = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var v = raw.Trim().ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "on";
        }

        private static int ParseInt(string? raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            return int.TryParse(raw, out var value) ? value : fallback;
        }

        private static string ResolveClojureEntryPath()
        {
            var explicitPath = Environment.GetEnvironmentVariable("CORECLR_STUDIO_CLJ_PATH");
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                var expanded = Environment.ExpandEnvironmentVariables(explicitPath);
                if (File.Exists(expanded)) return expanded;
            }

            var baseDirectory = AppContext.BaseDirectory;
            var projectDirectory = Directory.GetCurrentDirectory();

            var candidates = new[]
            {
                Path.Combine(projectDirectory, "src", "app", "core.clj"),
                Path.Combine(projectDirectory, "core.clj"),
                Path.Combine(baseDirectory, "src", "app", "core.clj"),
                Path.Combine(baseDirectory, "app", "core.clj"),
                Path.Combine(baseDirectory, "core.clj")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            return string.Empty;
        }

        private static void PreloadClojureAssemblies()
        {
            var baseDir = AppContext.BaseDirectory;
            var clj = Path.Combine(baseDir, "Clojure.dll");
            var cljSource = Path.Combine(baseDir, "Clojure.Source.dll");
            var specter = Path.Combine(baseDir, "com.rpl.specter.dll");
            var async = Path.Combine(baseDir, "clojure.core.async.dll");
            var analyzer = Path.Combine(baseDir, "clojure.tools.analyzer.dll");
            var reader = Path.Combine(baseDir, "clojure.tools.reader.dll");
            var analyzerClr = Path.Combine(baseDir, "clojure.tools.analyzer.clr.dll");
            var priorityMap = Path.Combine(baseDir, "clojure.data.priority-map.dll");
            var coreCache = Path.Combine(baseDir, "clojure.core.cache.dll");
            var coreMemoize = Path.Combine(baseDir, "clojure.core.memoize.dll");

            if (File.Exists(clj)) Assembly.LoadFrom(clj);
            if (File.Exists(cljSource)) Assembly.LoadFrom(cljSource);
            if (File.Exists(specter)) Assembly.LoadFrom(specter);
            if (File.Exists(async)) Assembly.LoadFrom(async);
            if (File.Exists(analyzer)) Assembly.LoadFrom(analyzer);
            if (File.Exists(reader)) Assembly.LoadFrom(reader);
            if (File.Exists(analyzerClr)) Assembly.LoadFrom(analyzerClr);
            if (File.Exists(priorityMap)) Assembly.LoadFrom(priorityMap);
            if (File.Exists(coreCache)) Assembly.LoadFrom(coreCache);
            if (File.Exists(coreMemoize)) Assembly.LoadFrom(coreMemoize);
        }

        private static void BootstrapClojureLoaders(string baseDir)
        {
            var escaped = baseDir.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var script = @$"(doseq [a [""clojure.tools.analyzer.dll"" ""clojure.tools.reader.dll"" ""clojure.data.priority-map.dll"" ""clojure.core.cache.dll"" ""clojure.core.memoize.dll"" ""clojure.tools.analyzer.clr.dll"" ""clojure.core.async.dll""]]
  (let [path (System.IO.Path/Combine ""{escaped}"" a)]
    (when (System.IO.File/Exists path)
      (assembly-load-file path))))";

            RT.var("clojure.core", "load-string").invoke(script);
        }
    }
}

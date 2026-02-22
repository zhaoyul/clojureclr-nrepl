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
            var repoRoot = FindRepoRoot();
            var cljPath = Path.Combine(repoRoot, "examples", "starter", "src", "app", "core.clj");

            PreloadClojureAssemblies();

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

        private static string FindRepoRoot()
        {
            var candidates = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

            foreach (var start in candidates)
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var marker = Path.Combine(dir.FullName, "clojureCLR-nrepl.csproj");
                    if (File.Exists(marker)) return dir.FullName;
                    dir = dir.Parent;
                }
            }

            throw new InvalidOperationException("Cannot locate repo root (clojureCLR-nrepl.csproj).");
        }

        private static void PreloadClojureAssemblies()
        {
            var baseDir = AppContext.BaseDirectory;
            var clj = Path.Combine(baseDir, "Clojure.dll");
            var cljSource = Path.Combine(baseDir, "Clojure.Source.dll");
            var specter = Path.Combine(baseDir, "com.rpl.specter.dll");

            if (File.Exists(clj)) Assembly.LoadFrom(clj);
            if (File.Exists(cljSource)) Assembly.LoadFrom(cljSource);
            if (File.Exists(specter)) Assembly.LoadFrom(specter);
        }
    }
}

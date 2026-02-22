using System;
using System.IO;
using System.Reflection;
using clojure.lang;
using clojureCLR_nrepl;
using Newtonsoft.Json.Linq;

namespace ClojureClrCSharpHost
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var repoRoot = FindRepoRoot();
            var cljPath = Path.Combine(repoRoot, "examples", "csharp-host", "src", "demo", "core.clj");

            PreloadClojureAssemblies();

            var server = new NReplServer("127.0.0.1", 1667);
            server.Start();

            try
            {
                Console.WriteLine("nREPL server started on 127.0.0.1:1667");
                Console.WriteLine("Loading Clojure file:");
                Console.WriteLine($"  {cljPath}");

                // Bootstrap RT/Core before touching Compiler directly.
                RT.var("clojure.core", "+");

                Compiler.loadFile(cljPath);

                var greet = RT.var("demo.core", "greet");
                var specterDemo = RT.var("demo.core", "specter_demo");

                Console.WriteLine($"greet => {greet.invoke("world")}");
                Console.WriteLine($"specter_demo => {specterDemo.invoke()}");

                var json = JObject.Parse("{\"ok\":true,\"n\":42}");
                Console.WriteLine($"json ok => {json["ok"]}, n => {json["n"]}");
            }
            finally
            {
                server.Stop();
            }
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

using System;
using System.IO;
using System.Reflection;
using clojure.lang;
using clojureCLR_nrepl;

namespace clojureclr_demo
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = Environment.GetEnvironmentVariable("NREPL_HOST") ?? "127.0.0.1";
            var portStr = Environment.GetEnvironmentVariable("NREPL_PORT") ?? "1667";
            var port = int.TryParse(portStr, out var p) ? p : 1667;

            var server = new NReplServer(host, port);
            server.Start();

            Console.WriteLine($"nREPL server started on {host}:{port}");

            // Load clojure.* assemblies so require can resolve resources.
            var baseDir = AppContext.BaseDirectory;
            foreach (var path in Directory.GetFiles(baseDir, "clojure.*.dll"))
            {
                try { Assembly.LoadFrom(path); } catch { }
            }

            // Try loading core.async (package example). If unavailable, continue with demo.
            try
            {
                var requireAsync = RT.var("clojure.core", "require");
                requireAsync.invoke(Symbol.create("clojure.core.async"));
                Console.WriteLine("clojure.core.async loaded.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"clojure.core.async not available: {ex.Message}");
            }

            var demoFile = Path.Combine(AppContext.BaseDirectory, "src", "demo", "core.clj");
            var loadFile = RT.var("clojure.core", "load-file");
            loadFile.invoke(demoFile);

            var require = RT.var("clojure.core", "require");
            require.invoke(Symbol.create("demo.core"));

            var runVar = RT.var("demo.core", "run");
            var result = runVar.invoke();
            Console.WriteLine($"demo.core/run => {result}");

            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();
            server.Stop();
        }
    }
}

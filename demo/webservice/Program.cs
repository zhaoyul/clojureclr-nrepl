using System;
using System.IO;
using System.Reflection;
using clojure.lang;

namespace clojureclr_webservice
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = Environment.GetEnvironmentVariable("WEB_HOST") ?? "127.0.0.1";
            var portStr = Environment.GetEnvironmentVariable("WEB_PORT") ?? "8080";
            var port = int.TryParse(portStr, out var p) ? p : 8080;

            // Load clojure.* assemblies so require can resolve resources.
            var baseDir = AppContext.BaseDirectory;
            foreach (var path in Directory.GetFiles(baseDir, "clojure.*.dll"))
            {
                try { Assembly.LoadFrom(path); } catch { }
            }
            // Ensure HttpListener assembly is loaded for Clojure type resolution.
            try { Assembly.Load("System.Net.HttpListener"); } catch { }

            var demoFile = Path.Combine(AppContext.BaseDirectory, "src", "demo", "web.clj");
            var loadFile = RT.var("clojure.core", "load-file");
            loadFile.invoke(demoFile);

            var require = RT.var("clojure.core", "require");
            require.invoke(Symbol.create("demo.web"));

            var start = RT.var("demo.web", "start!");
            start.invoke(host, port);

            Console.WriteLine($"webservice running on http://{host}:{port}/");
            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();

            var stop = RT.var("demo.web", "stop!");
            stop.invoke();
        }
    }
}

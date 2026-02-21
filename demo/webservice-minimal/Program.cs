using System;
using System.IO;
using System.Reflection;
using clojure.lang;

namespace clojureclr_webservice_minimal
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize ClojureCLR before ASP.NET loads additional assemblies.
            var baseDir = AppContext.BaseDirectory;
            foreach (var path in Directory.GetFiles(baseDir, "clojure.*.dll"))
            {
                try { Assembly.LoadFrom(path); } catch { }
            }
            try { Assembly.Load("System.Net.HttpListener"); } catch { }

            var cljFile = Path.Combine(baseDir, "src", "demo", "minimal.clj");
            var loadFile = RT.var("clojure.core", "load-file");
            loadFile.invoke(cljFile);

            var require = RT.var("clojure.core", "require");
            require.invoke(Symbol.create("demo.minimal"));

            var main = RT.var("demo.minimal", "-main");
            main.applyTo(RT.seq(args));
        }
    }
}

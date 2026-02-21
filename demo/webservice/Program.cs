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
            // Load clojure.* assemblies so require can resolve resources.
            var baseDir = AppContext.BaseDirectory;
            foreach (var path in Directory.GetFiles(baseDir, "clojure.*.dll"))
            {
                try { Assembly.LoadFrom(path); } catch { }
            }
            try { Assembly.Load("System.Net.HttpListener"); } catch { }

            var demoFile = Path.Combine(AppContext.BaseDirectory, "src", "demo", "web.clj");
            var loadFile = RT.var("clojure.core", "load-file");
            loadFile.invoke(demoFile);

            var require = RT.var("clojure.core", "require");
            require.invoke(Symbol.create("demo.web"));

            var main = RT.var("demo.web", "-main");
            main.applyTo(RT.seq(args));
        }
    }
}

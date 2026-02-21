(ns demo.run-core
  (:require [clojure.string :as str])
  (:import [System Environment Console Activator]
           [System.IO Directory FileInfo Path]
           [System.Reflection Assembly]))

(defn- find-repo-root [start-dir]
  (when (and start-dir (not= start-dir ""))
    (loop [dir (Path/GetFullPath start-dir)]
      (let [marker (Path/Combine dir "clojureCLR-nrepl.csproj")
            parent (Path/GetDirectoryName dir)]
        (cond
          (.Exists (FileInfo. marker)) dir
          (or (nil? parent) (= parent dir)) nil
          :else (recur parent))))))

(defn- repo-root []
  (let [from-file (when *file* (Path/GetDirectoryName *file*))
        from-cwd (Directory/GetCurrentDirectory)]
    (or (when from-file (find-repo-root from-file))
        (find-repo-root from-cwd)
        (throw (ex-info "Cannot locate repo root (clojureCLR-nrepl.csproj)" {})))))

(defn- nuget-root []
  (or (Environment/GetEnvironmentVariable "NUGET_PACKAGES")
      (let [home (or (Environment/GetEnvironmentVariable "HOME")
                     (Environment/GetEnvironmentVariable "USERPROFILE"))]
        (when (nil? home)
          (throw (ex-info "HOME/USERPROFILE not set; cannot locate NuGet cache" {})))
        (Path/Combine home ".nuget" "packages"))))

(defn- load-assembly! [path]
  (when-not (.Exists (FileInfo. path))
    (throw (ex-info (str "Assembly not found: " path) {:path path})))
  (Assembly/LoadFrom path))

(defn- combine-path [& parts]
  (reduce (fn [a b] (Path/Combine a b)) (first parts) (rest parts)))

(defn- load-package-assembly! [package-id version assembly-name]
  (let [root (nuget-root)
        path (combine-path root package-id version "lib" "netstandard2.0" assembly-name)]
    (load-assembly! path)))

(defn- load-local-assembly! [relative-paths]
  (let [root (repo-root)
        candidates (map #(Path/Combine root %) relative-paths)
        existing (first (filter #(.Exists (FileInfo. %)) candidates))]
    (when-not existing
      (throw (ex-info "Local assembly not found" {:candidates candidates})))
    (load-assembly! existing)))

(defn- parse-int [s default]
  (try
    (int (System.Int32/Parse s))
    (catch Exception _ default)))

(defn -main [& _]
  (try
    ;; Load local nREPL server assembly (build output)
    (let [nrepl-asm (load-local-assembly!
                      ["bin/Debug/net8.0/clojureCLR-nrepl.dll"
                       "bin/Release/net8.0/clojureCLR-nrepl.dll"])
          nrepl-type (.GetType nrepl-asm "clojureCLR_nrepl.NReplServer")]
      (when (nil? nrepl-type)
        (throw (ex-info "Type not found: clojureCLR_nrepl.NReplServer" {})))

      ;; Load NuGet assemblies for core.async and its analyzer dependency
      (load-package-assembly! "clojure.tools.analyzer" "1.1.1" "clojure.tools.analyzer.dll")
      (load-package-assembly! "clojure.tools.reader" "1.3.7" "clojure.tools.reader.dll")
      (load-package-assembly! "clojure.data.priority-map" "1.2.0" "clojure.data.priority-map.dll")
      (load-package-assembly! "clojure.core.cache" "1.1.234" "clojure.core.cache.dll")
      (load-package-assembly! "clojure.core.memoize" "1.1.266" "clojure.core.memoize.dll")
      (load-package-assembly! "clojure.tools.analyzer.clr" "1.3.2" "clojure.tools.analyzer.clr.dll")
      (load-package-assembly! "clojure.core.async.clrfix" "1.7.701-clrfix2" "clojure.core.async.dll")

      ;; Load demo code
      (let [root (repo-root)
            core-file (combine-path root "demo" "src" "demo" "core.clj")]
        (clojure.core/load-file core-file))

      (require 'demo.core)

      (let [host (or (Environment/GetEnvironmentVariable "NREPL_HOST") "127.0.0.1")
            port (parse-int (or (Environment/GetEnvironmentVariable "NREPL_PORT") "1667") 1667)
            server (Activator/CreateInstance nrepl-type (object-array [host port]))]
        (.Invoke (.GetMethod nrepl-type "Start") server (object-array []))
        (println (str "nREPL server started on " host ":" port))
        (let [run-var (ns-resolve 'demo.core 'run)]
          (when (nil? run-var)
            (throw (ex-info "Cannot resolve demo.core/run" {})))
          (println (str "demo.core/run => " (run-var))))
        (println "Press Enter to stop...")
        (Console/ReadLine)
        (.Invoke (.GetMethod nrepl-type "Stop") server (object-array []))))
    (catch Exception ex
      (println (str "ERROR: " (.Message ex)))
      (println (.ToString ex)))))

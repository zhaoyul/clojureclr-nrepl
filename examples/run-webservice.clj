(ns demo.run-webservice
  (:require [clojure.string :as str])
  (:import [System Environment Console]
           [System.IO Directory FileInfo Path]
           [System.Reflection Assembly]
           [System.Runtime.InteropServices RuntimeEnvironment]))

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

(defn- load-assembly! [path]
  (when-not (.Exists (FileInfo. path))
    (throw (ex-info (str "Assembly not found: " path) {:path path})))
  (Assembly/LoadFrom path))

(defn- load-runtime-assembly! [assembly-name]
  (try
    (Assembly/Load assembly-name)
    (catch Exception _
      (let [runtime-dir (RuntimeEnvironment/GetRuntimeDirectory)
            path (Path/Combine runtime-dir (str assembly-name ".dll"))]
        (load-assembly! path)))))

(defn- combine-path [& parts]
  (reduce (fn [a b] (Path/Combine a b)) (first parts) (rest parts)))

(defn- parse-int [s default]
  (try
    (int (System.Int32/Parse s))
    (catch Exception _ default)))

(defn- nrepl-enabled? [default-on?]
  (let [raw (Environment/GetEnvironmentVariable "NREPL_ENABLE")]
    (if (nil? raw)
      default-on?
      (contains? #{"1" "true" "yes" "on"} (str/lower-case raw)))))

(defonce ^:private nrepl* (atom nil))

(defn -main [& _]
  (load-runtime-assembly! "System.Net.HttpListener")
  (let [root (repo-root)
        nrepl-util-file (combine-path root "examples" "nrepl_util.clj")
        web-file (combine-path root "examples" "webservice" "src" "demo" "web.clj")]
    (clojure.core/load-file nrepl-util-file)
    (require 'demo.nrepl-util)
    (clojure.core/load-file web-file)
    (require 'demo.web)
    (let [host (or (Environment/GetEnvironmentVariable "WEB_HOST") "127.0.0.1")
          port (parse-int (or (Environment/GetEnvironmentVariable "WEB_PORT") "8080") 8080)
          start-var (ns-resolve 'demo.web 'start!)
          stop-var (ns-resolve 'demo.web 'stop!)]
      (when (nil? start-var)
        (throw (ex-info "Cannot resolve demo.web/start!" {})))
      (when (nil? stop-var)
        (throw (ex-info "Cannot resolve demo.web/stop!" {})))
      (let [start-nrepl (ns-resolve 'demo.nrepl-util 'start-nrepl!)
            stop-nrepl (ns-resolve 'demo.nrepl-util 'stop-nrepl!)]
        (when (nil? start-nrepl)
          (throw (ex-info "Cannot resolve demo.nrepl-util/start-nrepl!" {})))
        (when (nil? stop-nrepl)
          (throw (ex-info "Cannot resolve demo.nrepl-util/stop-nrepl!" {})))
        (when (nrepl-enabled? false)
          (let [nrepl-host (or (Environment/GetEnvironmentVariable "NREPL_HOST") "127.0.0.1")
                nrepl-port (parse-int (or (Environment/GetEnvironmentVariable "NREPL_PORT") "1667") 1667)
                server (start-nrepl root nrepl-host nrepl-port)]
            (reset! nrepl* server)
            (println (str "nREPL server started on " nrepl-host ":" nrepl-port))
            (println "Connect using:")
            (println (str "  lein repl :connect " nrepl-host ":" nrepl-port))
            (println "  Calva: Connect to Running nREPL Server")
            (println "  CIDER: cider-connect-clj")))
        (try
          (start-var host port)
          (println (str "webservice running on http://" host ":" port "/"))
          (println "Press Enter to stop...")
          (Console/ReadLine)
          (finally
            (stop-var)
            (when-let [server @nrepl*]
              (stop-nrepl server))))))))

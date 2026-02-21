(ns demo.run-repl-only
  (:import [System Environment Console]
           [System.IO Directory FileInfo Path]))

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

(defn- combine-path [& parts]
  (reduce (fn [a b] (Path/Combine a b)) (first parts) (rest parts)))

(defn- parse-int [s default]
  (try
    (int (System.Int32/Parse s))
    (catch Exception _ default)))

(defn -main [& _]
  (let [root (repo-root)
        nrepl-util-file (combine-path root "demo" "nrepl_util.clj")
        host (or (Environment/GetEnvironmentVariable "NREPL_HOST") "127.0.0.1")
        port (parse-int (or (Environment/GetEnvironmentVariable "NREPL_PORT") "1667") 1667)]
    (clojure.core/load-file nrepl-util-file)
    (require 'demo.nrepl-util)
    (let [start-nrepl (ns-resolve 'demo.nrepl-util 'start-nrepl!)
          stop-nrepl (ns-resolve 'demo.nrepl-util 'stop-nrepl!)]
      (when (nil? start-nrepl)
        (throw (ex-info "Cannot resolve demo.nrepl-util/start-nrepl!" {})))
      (when (nil? stop-nrepl)
        (throw (ex-info "Cannot resolve demo.nrepl-util/stop-nrepl!" {})))
      (let [server (start-nrepl root host port)]
        (println (str "nREPL server started on " host ":" port))
        (println "Connect using:")
        (println (str "  lein repl :connect " host ":" port))
        (println "  Calva: Connect to Running nREPL Server")
        (println "  CIDER: cider-connect-clj")
        (println "Press Enter to stop...")
        (Console/ReadLine)
        (stop-nrepl server)))))

(ns demo.run-specter
  (:require [clojure.string :as str])
  (:import [System Environment]
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

(defn- nrepl-enabled? [default-on?]
  (let [raw (Environment/GetEnvironmentVariable "NREPL_ENABLE")]
    (if (nil? raw)
      default-on?
      (contains? #{"1" "true" "yes" "on"} (str/lower-case raw)))))

(defn- parse-int [s default]
  (try
    (int (System.Int32/Parse s))
    (catch Exception _ default)))

(defonce ^:private nrepl* (atom nil))

(defn -main [& _]
  (let [root (repo-root)
        nrepl-util-file (combine-path root "examples" "nrepl_util.clj")
        specter-file (combine-path root "examples" "specter-demo" "src" "demo" "specter.clj")]
    (clojure.core/load-file nrepl-util-file)
    (require 'demo.nrepl-util)
    (let [start-nrepl (ns-resolve 'demo.nrepl-util 'start-nrepl!)
          stop-nrepl (ns-resolve 'demo.nrepl-util 'stop-nrepl!)]
      (when (nil? start-nrepl)
        (throw (ex-info "Cannot resolve demo.nrepl-util/start-nrepl!" {})))
      (when (nil? stop-nrepl)
        (throw (ex-info "Cannot resolve demo.nrepl-util/stop-nrepl!" {})))
      (when (nrepl-enabled? false)
        (let [host (or (Environment/GetEnvironmentVariable "NREPL_HOST") "127.0.0.1")
              port (parse-int (or (Environment/GetEnvironmentVariable "NREPL_PORT") "1667") 1667)
              server (start-nrepl root host port)]
          (reset! nrepl* server)
          (println (str "nREPL server started on " host ":" port))
          (println "Connect using:")
          (println (str "  lein repl :connect " host ":" port))
          (println "  Calva: Connect to Running nREPL Server")
          (println "  CIDER: cider-connect-clj")))

      (load-package-assembly! "com.rpl.specter.clr" "1.1.7-clrfix1" "com.rpl.specter.dll")
      (clojure.core/load-file specter-file)))
  (require 'demo.specter)
  (let [run-var (ns-resolve 'demo.specter 'run)]
    (when (nil? run-var)
      (throw (ex-info "Cannot resolve demo.specter/run" {})))
    (prn (run-var)))
  (when-let [server @nrepl*]
    (let [stop-nrepl (ns-resolve 'demo.nrepl-util 'stop-nrepl!)]
      (when stop-nrepl
        (stop-nrepl server)))))

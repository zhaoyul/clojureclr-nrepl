(ns demo.nrepl-util
  (:import [System Activator]
           [System.IO FileInfo Path]
           [System.Reflection Assembly]))

(defn- combine-path [& parts]
  (reduce (fn [a b] (Path/Combine a b)) (first parts) (rest parts)))

(defn- load-assembly! [path]
  (when-not (.Exists (FileInfo. path))
    (throw (ex-info (str "Assembly not found: " path) {:path path})))
  (Assembly/LoadFrom path))

(defn- load-local-nrepl-assembly! [root]
  (let [candidates [(combine-path root "bin" "Debug" "net8.0" "clojureCLR-nrepl.dll")
                    (combine-path root "bin" "Release" "net8.0" "clojureCLR-nrepl.dll")]
        existing (first (filter #(.Exists (FileInfo. %)) candidates))]
    (when-not existing
      (throw (ex-info "Local nREPL assembly not found" {:candidates candidates})))
    (load-assembly! existing)))

(defn start-nrepl! [root host port]
  (let [nrepl-asm (load-local-nrepl-assembly! root)
        nrepl-type (.GetType nrepl-asm "clojureCLR_nrepl.NReplServer")]
    (when (nil? nrepl-type)
      (throw (ex-info "Type not found: clojureCLR_nrepl.NReplServer" {})))
    (let [server (Activator/CreateInstance nrepl-type (object-array [host port]))]
      (.Invoke (.GetMethod nrepl-type "Start") server (object-array []))
      server)))

(defn stop-nrepl! [server]
  (when server
    (let [t (.GetType server)]
      (.Invoke (.GetMethod t "Stop") server (object-array [])))))

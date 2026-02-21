(ns demo.web
  (:import [System Console Environment]
           [System.Net HttpListener]
           [System.Text Encoding]
           [System.IO StreamReader]
           [System.Threading Thread]))

(defonce ^:private listener* (atom nil))
(defonce ^:private stop* (atom false))
(def ^:private default-host "127.0.0.1")
(def ^:private default-port 8080)

(defn- bytes-utf8 [^String s]
  (.GetBytes Encoding/UTF8 s))

(defn- respond! [resp status body content-type]
  (let [bytes (bytes-utf8 body)]
    (set! (.StatusCode resp) (int status))
    (set! (.ContentType resp) content-type)
    (set! (.ContentLength64 resp) (long (alength bytes)))
    (with-open [out (.OutputStream resp)]
      (.Write out bytes 0 (alength bytes)))))

(defn- read-body [req]
  (with-open [sr (StreamReader. (.InputStream req) Encoding/UTF8)]
    (.ReadToEnd sr)))

(defn- handle! [ctx]
  (let [req (.Request ctx)
        resp (.Response ctx)
        method (.HttpMethod req)
        path (.AbsolutePath (.Url req))]
    (try
      (cond
        (and (= method "GET") (= path "/"))
        (respond! resp 200 "hello from ClojureCLR" "text/plain; charset=utf-8")

        (and (= method "GET") (= path "/health"))
        (respond! resp 200 "{\"ok\":true}" "application/json; charset=utf-8")

        (and (= method "POST") (= path "/echo"))
        (let [body (read-body req)]
          (respond! resp 200 body "text/plain; charset=utf-8"))

        :else
        (respond! resp 404 "not found" "text/plain; charset=utf-8"))
      (catch Exception ex
        (respond! resp 500 (str "error: " (.Message ex)) "text/plain; charset=utf-8"))
      (finally
        (.Close resp)))))

(defn start!
  ([host port]
   (when @listener*
     (throw (ex-info "listener already started" {})))
   (let [listener (HttpListener.)
         hosts (if (#{"127.0.0.1" "localhost"} host)
                 ["127.0.0.1" "localhost"]
                 [host])]
     (doseq [h hosts]
       (let [prefix (format "http://%s:%d/" h (int port))]
         (.Add (.Prefixes listener) prefix)))
     (.Start listener)
     (reset! stop* false)
     (reset! listener* listener)
     (future
       (while (and (not @stop*) (.IsListening listener))
         (try
           (let [ctx (.GetContext listener)]
             (handle! ctx))
           (catch Exception _
             ;; Stop will break GetContext; ignore if shutting down
             (when-not @stop*
               (Thread/Sleep 10)))))))
   {:host host :port port})
  ([port]
   (start! default-host port))
  ([]
   (start! default-host default-port)))

(defn stop! []
  (reset! stop* true)
  (when-let [listener @listener*]
    (try
      (.Stop listener)
      (.Close listener)
      (catch Exception _))
    (reset! listener* nil)))

(defn- env-port []
  (or (some-> (Environment/GetEnvironmentVariable "WEB_PORT") parse-long)
      default-port))

(defn -main [& _]
  (let [host (or (Environment/GetEnvironmentVariable "WEB_HOST") default-host)
        port (env-port)]
    (start! host port)
    (println (format "webservice running on http://%s:%d/" host (int port)))
    (println "Press Enter to stop...")
    (Console/ReadLine)
    (stop!)))

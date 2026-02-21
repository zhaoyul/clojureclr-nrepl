(ns demo.minimal
  (:import [System Console Environment]
           [System.Net HttpListener]
           [System.Text Encoding]
           [System.IO StreamReader]
           [System.Threading Thread]))

(defn hello []
  "hello from ClojureCLR (minimal api)")

(defn health []
  "{\"ok\":true}")

(defn echo [s]
  (if (nil? s) "" s))

(defonce ^:private listener* (atom nil))
(defonce ^:private stop* (atom false))

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
        (respond! resp 200 (hello) "text/plain; charset=utf-8")

        (and (= method "GET") (= path "/health"))
        (respond! resp 200 (health) "application/json; charset=utf-8")

        (and (= method "POST") (= path "/echo"))
        (respond! resp 200 (echo (read-body req)) "text/plain; charset=utf-8")

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
   (let [listener (HttpListener.)]
     (.Add (.Prefixes listener) (format "http://%s:%d/" host (int port)))
     (.Start listener)
     (reset! stop* false)
     (reset! listener* listener)
     (future
       (while (and (not @stop*) (.IsListening listener))
         (try
           (handle! (.GetContext listener))
           (catch Exception _
             (when-not @stop*
               (Thread/Sleep 10)))))))
   {:host host :port port})
  ([]
   (start! "127.0.0.1" 8082)))

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
      8082))

(defn -main [& _]
  (let [host (or (Environment/GetEnvironmentVariable "WEB_HOST") "127.0.0.1")
        port (env-port)]
    (start! host port)
    (println (format "webservice running on http://%s:%d/" host (int port)))
    (println "Press Enter to stop...")
    (Console/ReadLine)
    (stop!)))

(require '[clojure.tools.reader :as tr]
         '[clojure.tools.reader.reader-types :as rr]
         '[clojure.java.io :as io]
         '[clojure.pprint :as pp]
         '[clojure.walk :as walk])

(def ^:private pos-meta-keys
  [:line :column :end-line :end-column :file :source-logical-path])

(declare scrub-node)

(defn- clean-meta [alias-map m]
  (when m
    (let [m' (not-empty (apply dissoc m pos-meta-keys))]
      (when m'
        (not-empty (walk/postwalk (partial scrub-node alias-map) m'))))))

(defn- replace-alias [alias-map x]
  (if (symbol? x)
    (let [ns-part (some-> (namespace x) symbol)
          target (when ns-part (get alias-map ns-part))]
      (if target
        (symbol (str target) (name x))
        x))
    x))

(defn- scrub-node [alias-map x]
  (let [x (if (instance? clojure.lang.IObj x)
            (with-meta x (clean-meta alias-map (meta x)))
            x)]
    (replace-alias alias-map x)))

(defn- scrub-form [alias-map form]
  (walk/postwalk (partial scrub-node alias-map) form))

(defn- read-next [opts r]
  (let [res (try [:ok (tr/read opts r)]
                 (catch Exception e [:err e]))]
    (if (= :ok (first res))
      (second res)
      (let [e (second res)
            data (ex-data e)]
        (if (= (:ex-kind data) :eof)
          ::eof
          (throw e))))))

(defn- ns-sym-from-form [form]
  (when (and (seq? form) (= 'ns (first form)))
    (second form)))

(defn- ns-clauses [ns-form]
  (filter seq? (drop 2 ns-form)))

(defn- require-aliases [ns-form]
  (for [clause (ns-clauses ns-form)
        :when (= :require (first clause))
        spec (rest clause)
        :let [specv (when (sequential? spec) (vec spec))
              as-idx (when specv (.indexOf specv :as))]
        :when (and specv (number? as-idx) (<= 0 as-idx) (< (inc as-idx) (count specv)))
        :let [ns-sym (nth specv 0)
              alias-sym (nth specv (inc as-idx))]]
    [ns-sym alias-sym]))

(defn- alias-uses [text alias]
  (let [re (re-pattern (str "\\b" (java.util.regex.Pattern/quote (name alias)) "/([A-Za-z0-9_\\-\\*\\?\\!\\+\\<\\>\\=]+)"))]
    (->> (re-seq re text)
         (map second)
         (map symbol)
         set)))

(defn read-forms [path]
  (let [text (slurp path)
        opts {:read-cond :allow :features #{:cljr}}]
    (with-open [r (rr/indexing-push-back-reader (io/reader path))]
      (let [first-form (read-next opts r)]
      (when (= first-form ::eof)
        (throw (ex-info "Empty file" {:path path})))
      (let [ns-sym (ns-sym-from-form first-form)
            ns-obj (when ns-sym (or (find-ns ns-sym) (create-ns ns-sym)))]
        (when ns-obj
          (binding [*ns* ns-obj]
            (refer 'clojure.core)
            (doseq [[req-ns alias-sym] (require-aliases first-form)]
              (let [req-obj (or (find-ns req-ns) (create-ns req-ns))]
                (clojure.core/alias alias-sym req-obj)
                ;; create stub vars for alias-qualified symbols so syntax-quote can resolve them
                (doseq [sym (alias-uses text alias-sym)]
                  (when-not (resolve (symbol (str (ns-name req-obj)) (name sym)))
                    (intern req-obj sym)))))))
        (let [alias-map (into {} (map (fn [[req alias-sym]] [alias-sym req])
                                      (require-aliases first-form)))]
          (binding [*ns* (or ns-obj *ns*)]
            (loop [acc [(scrub-form alias-map first-form)]]
              (let [form (read-next opts r)]
                (if (= form ::eof)
                  acc
                  (recur (conj acc (scrub-form alias-map form)))))))))))))

(defn write-forms [path forms]
  (with-open [w (io/writer path)]
    (binding [*out* w
              *print-meta* true
              *print-length* nil
              *print-level* nil]
      (doseq [f forms]
        (pp/pprint f)
        (println)))))

(defn ->clj-path [p]
  (if (.endsWith p ".cljc")
    (str (subs p 0 (- (count p) 1)))
    p))

(def files
  (->> (concat
         ["specter-clr/src/clj/com/rpl/specter.cljc"]
         (sort (map str (file-seq (io/file "specter-clr/src/clj/com/rpl/specter")))) )
       (filter #(.endsWith ^String % ".cljc"))))

(doseq [f files]
  (let [out (->clj-path f)
        forms (read-forms f)]
    (write-forms out forms)
    (println "Wrote" out "(" (count forms) "forms )")))

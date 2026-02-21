(ns
 com.rpl.specter.impl
 (:use
  [com.rpl.specter.protocols
   :only
   [select* transform* collect-val RichNavigator]]
  [com.rpl.specter.util-macros :only [doseqres mk-comp-navs]])
 (:require
  [com.rpl.specter.protocols :as p]
  [clojure.pprint :as pp]
  [clojure.string :as s]
  [clojure.walk :as walk]
  [clojure.walk :as riddley]))

(def NONE :com.rpl.specter.impl/NONE)

(defn spy [e] (println "SPY:") (println (pr-str e)) e)

(defn- smart-str* [o] (if (coll? o) (pr-str o) (str o)))

(defn ^String smart-str [& elems] (apply str (map smart-str* elems)))

(defn
 fast-constantly
 [v]
 (fn
  ([] v)
  ([a1] v)
  ([a1 a2] v)
  ([a1 a2 a3] v)
  ([a1 a2 a3 a4] v)
  ([a1 a2 a3 a4 a5] v)
  ([a1 a2 a3 a4 a5 a6] v)
  ([a1 a2 a3 a4 a5 a6 a7] v)
  ([a1 a2 a3 a4 a5 a6 a7 a8] v)
  ([a1 a2 a3 a4 a5 a6 a7 a8 a9] v)
  ([a1 a2 a3 a4 a5 a6 a7 a8 a9 a10] v)
  ([a1 a2 a3 a4 a5 a6 a7 a8 a9 a10 & r] v)))

(defn
 cljs-analyzer-macroexpand-1
 []
 (throw (ex-info "cljs.analyzer not available on ClojureCLR" {})))

(defn clj-macroexpand-all [form] (clojure.walk/macroexpand-all form))

(defn intern* [ns name val] (intern ns name val))

(defmacro
 fast-object-array
 [i]
 (clojure.core/sequence
  (clojure.core/seq
   (clojure.core/concat
    (clojure.core/list 'clojure.core/object-array)
    (clojure.core/list i)))))

(defn
 array-copy-of
 [arr new-len]
 (let
  [n
   (int new-len)
   out
   (object-array n)
   max-len
   (alength arr)
   copy-len
   (if (< max-len n) max-len n)]
  (System.Array/Copy arr out copy-len)
  out))

(defn benchmark [iters afn] (time (dotimes [_ iters] (afn))))

(defmacro
 exec-select*
 [this & args]
 (clojure.core/sequence
  (clojure.core/seq
   (clojure.core/concat
    (clojure.core/list 'com.rpl.specter.protocols/select*)
    (clojure.core/list this)
    args))))

(defmacro
 exec-transform*
 [this & args]
 (clojure.core/sequence
  (clojure.core/seq
   (clojure.core/concat
    (clojure.core/list 'com.rpl.specter.protocols/transform*)
    (clojure.core/list this)
    args))))

(defprotocol PathComposer (do-comp-paths [paths]))

(defn
 rich-nav?
 [n]
 (instance? com.rpl.specter.protocols.RichNavigator n))

(defn comp-paths* [p] (if (rich-nav? p) p (do-comp-paths p)))

(defn-
 coerce-object
 [this]
 (cond
  (rich-nav? this)
  this
  (satisfies? com.rpl.specter.protocols/ImplicitNav this)
  (com.rpl.specter.protocols/implicit-nav this)
  :else
  (throw
   (ex-info
    "Not a navigator"
    {:this this, :type-str (pr-str (type this))}))))

(defprotocol CoercePath (coerce-path [this]))

(extend-protocol
 CoercePath
 nil
 (coerce-path [this] (coerce-object this))
 clojure.lang.IPersistentVector
 (coerce-path [this] (do-comp-paths this))
 Object
 (coerce-path [this] (coerce-object this)))

(def
 STAY*
 (reify
  RichNavigator
  (select* [this vals structure next-fn] (next-fn vals structure))
  (transform* [this vals structure next-fn] (next-fn vals structure))))

(defn
 combine-two-navs
 [nav1 nav2]
 (reify
  RichNavigator
  (select*
   [this vals structure next-fn]
   (exec-select*
    nav1
    vals
    structure
    (fn
     [vals-next structure-next]
     (exec-select* nav2 vals-next structure-next next-fn))))
  (transform*
   [this vals structure next-fn]
   (exec-transform*
    nav1
    vals
    structure
    (fn
     [vals-next structure-next]
     (exec-transform* nav2 vals-next structure-next next-fn))))))

(extend-protocol
 PathComposer
 nil
 (do-comp-paths [o] (coerce-path o))
 Object
 (do-comp-paths [o] (coerce-path o))
 clojure.lang.IPersistentVector
 (do-comp-paths
  [navigators]
  (let
   [coerced (map coerce-path navigators)]
   (cond
    (empty? coerced)
    STAY*
    (= 1 (count coerced))
    (first coerced)
    :else
    (reduce combine-two-navs coerced)))))

(defprotocol PMutableCell (set_cell [cell x]))

(deftype
 MutableCell
 [^{:volatile-mutable true} q]
 PMutableCell
 (set_cell [this x] (set! q x)))

(defn mutable-cell ([] (mutable-cell nil)) ([init] (MutableCell. init)))

(defn set-cell! [cell val] (set_cell cell val))

(defn get-cell [cell] (.-q cell))

(defn
 update-cell!
 [cell afn]
 (let [ret (afn (get-cell cell))] (set-cell! cell ret) ret))

(defmacro
 compiled-traverse-with-vals*
 [path result-fn vals structure]
 (clojure.core/sequence
  (clojure.core/seq
   (clojure.core/concat
    (clojure.core/list 'com.rpl.specter.impl/exec-select*)
    (clojure.core/list path)
    (clojure.core/list vals)
    (clojure.core/list structure)
    (clojure.core/list
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'clojure.core/fn)
        (clojure.core/list
         (clojure.core/vec
          (clojure.core/sequence
           (clojure.core/seq
            (clojure.core/concat
             (clojure.core/list 'vals__1122__auto__)
             (clojure.core/list 'structure__1123__auto__))))))
        (clojure.core/list
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'if)
            (clojure.core/list
             (clojure.core/sequence
              (clojure.core/seq
               (clojure.core/concat
                (clojure.core/list 'clojure.core/identical?)
                (clojure.core/list 'vals__1122__auto__)
                (clojure.core/list
                 (clojure.core/vec
                  (clojure.core/sequence
                   (clojure.core/seq (clojure.core/concat)))))))))
            (clojure.core/list
             (clojure.core/sequence
              (clojure.core/seq
               (clojure.core/concat
                (clojure.core/list result-fn)
                (clojure.core/list 'structure__1123__auto__)))))
            (clojure.core/list
             (clojure.core/sequence
              (clojure.core/seq
               (clojure.core/concat
                (clojure.core/list result-fn)
                (clojure.core/list
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'clojure.core/conj)
                    (clojure.core/list 'vals__1122__auto__)
                    (clojure.core/list
                     'structure__1123__auto__)))))))))))))))))))))

(defn
 compiled-traverse*
 [path result-fn structure]
 (compiled-traverse-with-vals* path result-fn [] structure))

(defn
 do-compiled-traverse*
 [apath structure]
 (reify
  clojure.lang.IReduce
  (reduce [this afn] (.reduce this afn (afn)))
  (reduce
   [this afn start]
   (let
    [cell (mutable-cell start)]
    (compiled-traverse*
     apath
     (fn
      [elem]
      (let
       [curr (get-cell cell) newv (afn curr elem)]
       (set-cell! cell newv)
       newv))
     structure)
    (get-cell cell)))))

(defn-
 call-reduce-interface
 [^clojure.lang.IReduce traverser afn start]
 (.reduce traverser afn start))

(defn
 do-compiled-traverse
 [apath structure]
 (let
  [traverser (do-compiled-traverse* apath structure)]
  (reify
   clojure.lang.IReduce
   (reduce [this afn] (.reduce this afn (afn)))
   (reduce
    [this afn start]
    (let
     [res (call-reduce-interface traverser afn start)]
     (unreduced res))))))

(defn
 compiled-traverse-all*
 [path]
 (fn
  [xf]
  (fn
   ([] (xf))
   ([result] (xf result))
   ([result input]
    (reduce
     (fn [r i] (xf r i))
     result
     (do-compiled-traverse* path input))))))

(defn
 compiled-select*
 [path structure]
 (let
  [res
   (mutable-cell (transient []))
   result-fn
   (fn
    [structure]
    (let
     [curr (get-cell res)]
     (set-cell! res (conj! curr structure))))]
  (compiled-traverse* path result-fn structure)
  (persistent! (get-cell res))))

(defn
 compiled-select-one*
 [path structure]
 (let
  [res
   (mutable-cell NONE)
   result-fn
   (fn
    [structure]
    (let
     [curr (get-cell res)]
     (if
      (identical? curr NONE)
      (set-cell! res structure)
      (throw
       (ex-info
        "More than one element found in structure"
        {:structure structure})))))]
  (compiled-traverse* path result-fn structure)
  (let [ret (get-cell res)] (if (identical? ret NONE) nil ret))))

(defn
 compiled-select-one!*
 [path structure]
 (let
  [res
   (mutable-cell NONE)
   result-fn
   (fn
    [structure]
    (let
     [curr (get-cell res)]
     (if
      (identical? curr NONE)
      (set-cell! res structure)
      (throw
       (ex-info
        "More than one element found in structure"
        {:structure structure})))))]
  (compiled-traverse* path result-fn structure)
  (let
   [ret (get-cell res)]
   (if
    (identical? NONE ret)
    (throw
     (ex-info
      "Found no elements for select-one!"
      {:structure structure})))
   ret)))

(defn
 compiled-select-any*
 ([path structure] (compiled-select-any* path [] structure))
 ([path vals structure]
  (unreduced
   (compiled-traverse-with-vals* path reduced vals structure))))

(defn
 compiled-select-first*
 [path structure]
 (let
  [ret (compiled-select-any* path structure)]
  (if (identical? ret NONE) nil ret)))

(defn
 compiled-selected-any?*
 [path structure]
 (not (identical? NONE (compiled-select-any* path structure))))

(defn
 terminal*
 [afn vals structure]
 (if
  (identical? vals [])
  (afn structure)
  (apply afn (conj vals structure))))

(defn
 compiled-transform*
 [nav transform-fn structure]
 (exec-transform*
  nav
  []
  structure
  (fn [vals structure] (terminal* transform-fn vals structure))))

(defn
 compiled-vtransform*
 [nav transform-fn structure]
 (exec-transform* nav [] structure transform-fn))

(defn
 fn-invocation?
 [f]
 (or
  (instance? clojure.lang.Cons f)
  (instance? clojure.lang.LazySeq f)
  (list? f)))

(defrecord LocalSym [val sym])

(defrecord VarUse [val avar sym])

(defrecord SpecialFormUse [val code])

(defrecord FnInvocation [op params code])

(defrecord DynamicVal [code])

(defrecord DynamicPath [path])

(defrecord DynamicFunction [op params code])

(defn
 dynamic-param?
 [o]
 (contains? #{DynamicPath DynamicFunction DynamicVal} (type o)))

(defn
 static-path?
 [path]
 (if
  (sequential? path)
  (every? static-path? path)
  (-> path dynamic-param? not)))

(defn
 late-path
 [path]
 (if
  (static-path? path)
  (comp-paths* path)
  (com.rpl.specter.impl/->DynamicPath path)))

(defrecord CachedPathInfo [dynamic? precompiled])

(defn
 cached-path-info-precompiled
 [^CachedPathInfo c]
 (.-precompiled c))

(defn cached-path-info-dynamic? [^CachedPathInfo c] (.-dynamic? c))

(defn
 filter-select
 [afn vals structure next-fn]
 (if (afn structure) (next-fn vals structure) NONE))

(defn
 filter-transform
 [afn vals structure next-fn]
 (if (afn structure) (next-fn vals structure) structure))

(defn
 ^{:direct-nav true} pred*
 [afn]
 (reify
  RichNavigator
  (select*
   [this vals structure next-fn]
   (if (afn structure) (next-fn vals structure) NONE))
  (transform*
   [this vals structure next-fn]
   (if (afn structure) (next-fn vals structure) structure))))

(defn
 ^{:direct-nav true} collected?*
 [afn]
 (reify
  RichNavigator
  (select*
   [this vals structure next-fn]
   (if (afn vals) (next-fn vals structure) NONE))
  (transform*
   [this vals structure next-fn]
   (if (afn vals) (next-fn vals structure) structure))))

(defn
 ^{:direct-nav true} cell-nav
 [cell]
 (reify
  RichNavigator
  (select*
   [this vals structure next-fn]
   (exec-select* (get-cell cell) vals structure next-fn))
  (transform*
   [this vals structure next-fn]
   (exec-transform* (get-cell cell) vals structure next-fn))))

(defn
 local-declarepath
 []
 (let
  [cell (mutable-cell nil)]
  (vary-meta (cell-nav cell) assoc :com.rpl.specter.impl/cell cell)))

(defn
 providepath*
 [declared compiled-path]
 (let
  [cell (-> declared meta :com.rpl.specter.impl/cell)]
  (set-cell! cell compiled-path)))

(defn- gensyms [amt] (vec (repeatedly amt gensym)))

(mk-comp-navs)

(defn
 srange-transform*
 [structure start end next-fn]
 (if
  (string? structure)
  (let
   [newss (next-fn (subs structure start end))]
   (str
    (subs structure 0 start)
    newss
    (subs structure end (count structure))))
  (let
   [structurev
    (vec structure)
    newpart
    (next-fn (-> structurev (subvec start end)))
    res
    (concat
     (subvec structurev 0 start)
     newpart
     (subvec structurev end (count structure)))]
   (if (vector? structure) (vec res) res))))

(defn-
 matching-indices
 [aseq p]
 (keep-indexed (fn [i e] (if (p e) i)) aseq))

(defn
 matching-ranges
 [aseq p]
 (first
  (reduce
   (fn
    [[ranges curr-start curr-last :as curr] i]
    (cond
     (nil? curr-start)
     [ranges i i]
     (= i (inc curr-last))
     [ranges curr-start i]
     :else
     [(conj ranges [curr-start (inc curr-last)]) i i]))
   [[] nil nil]
   (concat (matching-indices aseq p) [-1]))))

(defn
 continuous-subseqs-transform*
 [pred structure next-fn]
 (reduce
  (fn [structure [s e]] (srange-transform* structure s e next-fn))
  structure
  (reverse (matching-ranges structure pred))))

(defn
 codewalk-until
 [pred on-match-fn structure]
 (if
  (pred structure)
  (on-match-fn structure)
  (let
   [ret
    (clojure.walk/walk
     (partial codewalk-until pred on-match-fn)
     identity
     structure)]
   (if
    (and (fn-invocation? structure) (fn-invocation? ret))
    (with-meta ret (meta structure))
    ret))))

(defn
 walk-select
 [pred continue-fn structure]
 (let
  [ret
   (mutable-cell NONE)
   walker
   (fn
    this
    [structure]
    (if
     (pred structure)
     (let
      [r (continue-fn structure)]
      (if-not (identical? r NONE) (set-cell! ret r))
      r)
     (clojure.walk/walk this identity structure)))]
  (walker structure)
  (get-cell ret)))

(defn
 walk-until
 [pred on-match-fn structure]
 (if
  (pred structure)
  (on-match-fn structure)
  (clojure.walk/walk
   (partial walk-until pred on-match-fn)
   identity
   structure)))

(do
 (def ^{:dynamic true} *tmp-closure*)
 (defn
  closed-code
  [closure body]
  (let
   [lv
    (mapcat
     (fn*
      [p1__1130#]
      (vector
       p1__1130#
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'com.rpl.specter.impl/*tmp-closure*)
          (clojure.core/list
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list 'quote)
              (clojure.core/list p1__1130#))))))))))
     (keys closure))]
   (binding
    [*tmp-closure* closure]
    (eval
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'clojure.core/let)
        (clojure.core/list
         (clojure.core/vec
          (clojure.core/sequence
           (clojure.core/seq (clojure.core/concat lv)))))
        (clojure.core/list body))))))))
 (let
  [embeddable?
   (some-fn
    number?
    symbol?
    keyword?
    string?
    char?
    list?
    vector?
    set?
    (fn* [p1__1131#] (and (map? p1__1131#) (not (record? p1__1131#))))
    nil?
    (fn* [p1__1132#] (instance? clojure.lang.Cons p1__1132#))
    (fn* [p1__1133#] (instance? clojure.lang.LazySeq p1__1133#)))]
  (defn
   eval+
   "Automatically extracts non-evalable stuff into a closure and then evals"
   [form]
   (let
    [replacements
     (mutable-cell {})
     new-form
     (codewalk-until
      (fn* [p1__1134#] (-> p1__1134# embeddable? not))
      (fn
       [o]
       (let
        [s (gensym)]
        (update-cell!
         replacements
         (fn* [p1__1135#] (assoc p1__1135# s o)))
        s))
      form)
     closure
     (get-cell replacements)]
    (closed-code closure new-form)))))

(defn
 coerce-nav
 [o]
 (cond
  (instance? com.rpl.specter.protocols.RichNavigator o)
  o
  (sequential? o)
  (comp-paths* o)
  :else
  (com.rpl.specter.protocols/implicit-nav o)))

(defn dynamic-var? [v] (-> v meta :dynamic))

(defn
 direct-nav-obj
 [o]
 (vary-meta o merge {:direct-nav true, :original-obj o}))

(defn
 maybe-direct-nav
 [obj direct-nav?]
 (if direct-nav? (direct-nav-obj obj) obj))

(defn
 original-obj
 [o]
 (let [orig (-> o meta :original-obj)] (if orig (recur orig) o)))

(defn direct-nav? [o] (-> o meta :direct-nav))

(defn
 all-static?
 [params]
 (identical? NONE (walk-select dynamic-param? identity params)))

(defn
 late-resolved-fn
 [afn]
 (fn
  [& args]
  (if
   (all-static? args)
   (apply afn args)
   (->DynamicFunction afn args nil))))

(defn
 preserve-map
 [afn o]
 (if (or (list? o) (seq? o)) (map afn o) (into (empty o) (map afn o))))

(defn-
 magic-precompilation*
 [o]
 (cond
  (sequential? o)
  (preserve-map magic-precompilation* o)
  (instance? VarUse o)
  (let
   [v (:avar o)]
   (if
    (and v (dynamic-var? v))
    (->DynamicVal
     (maybe-direct-nav
      (:sym o)
      (or (direct-nav? v) (-> o :sym direct-nav?))))
    (maybe-direct-nav
     (:val o)
     (or
      (and v (direct-nav? v))
      (-> o :sym direct-nav?)
      (-> o :val direct-nav?)))))
  (instance? LocalSym o)
  (->DynamicVal (:sym o))
  (instance? SpecialFormUse o)
  (->DynamicVal (:code o))
  (instance? FnInvocation o)
  (let
   [op
    (magic-precompilation* (:op o))
    params
    (doall (map magic-precompilation* (:params o)))]
   (if
    (or (-> op meta :dynamicnav) (all-static? (conj params op)))
    (magic-precompilation* (apply op params))
    (->DynamicFunction op params (:code o))))
  :else
  o))

(defn
 static-combine
 ([o] (static-combine o true))
 ([o nav-pos?]
  (cond
   (sequential? o)
   (if
    nav-pos?
    (let
     [res
      (continuous-subseqs-transform*
       rich-nav?
       (doall (map static-combine (flatten o)))
       (fn [s] [(comp-paths* s)]))]
     (if (= 1 (count res)) (first res) res))
    (preserve-map
     (fn* [p1__1136#] (static-combine p1__1136# false))
     o))
   (instance? DynamicFunction o)
   (->DynamicFunction
    (static-combine (:op o) false)
    (doall
     (map
      (fn* [p1__1137#] (static-combine p1__1137# false))
      (:params o)))
    (:code o))
   (instance? DynamicPath o)
   (->DynamicPath (static-combine (:path o)))
   (instance? DynamicVal o)
   o
   :else
   (if nav-pos? (coerce-nav o) o))))

(defn
 static-fn-code
 [afn args]
 (clojure.core/sequence
  (clojure.core/seq
   (clojure.core/concat (clojure.core/list afn) args))))

(defn
 dynamic-fn-code
 [afn args]
 (clojure.core/sequence
  (clojure.core/seq
   (clojure.core/concat (clojure.core/list afn) args))))

(defn dynamic-val-code [code possible-params] code)

(defn static-val-code [o] o)

(declare resolve-nav-code)

(defn dynamic->code [o] (walk-until dynamic-param? :code o))

(defn
 resolve-arg-code
 [o possible-params]
 (cond
  (instance? DynamicFunction o)
  (let
   [op
    (resolve-arg-code (:op o) possible-params)
    params
    (map
     (fn* [p1__1138#] (resolve-arg-code p1__1138# possible-params))
     (:params o))]
   (maybe-direct-nav
    (dynamic-fn-code (original-obj op) params)
    (direct-nav? (:op o))))
  (instance? DynamicVal o)
  (dynamic-val-code (:code o) possible-params)
  (instance? DynamicPath o)
  (resolve-nav-code o possible-params)
  :else
  (if
   (identical? NONE (walk-select dynamic-param? identity o))
   (static-val-code o)
   (resolve-arg-code
    (->DynamicVal (dynamic->code o))
    possible-params))))

(defn
 resolve-nav-code
 [o possible-params]
 (cond
  (instance? DynamicPath o)
  (let
   [path (:path o)]
   (if
    (sequential? path)
    (let
     [resolved
      (vec
       (map
        (fn* [p1__1139#] (resolve-nav-code p1__1139# possible-params))
        path))]
     (cond
      (empty? resolved)
      (static-val-code STAY*)
      (= 1 (count resolved))
      (first resolved)
      :else
      (static-fn-code comp-navs resolved)))
    (resolve-nav-code path possible-params)))
  (instance? DynamicVal o)
  (let
   [code (:code o) d (dynamic-val-code code possible-params)]
   (cond
    (direct-nav? code)
    d
    (or (set? code) (and (fn-invocation? code) (= 'fn* (first code))))
    (static-fn-code pred* [d])
    :else
    (static-fn-code coerce-nav [d])))
  (instance? DynamicFunction o)
  (let
   [res (resolve-arg-code o possible-params)]
   (if (direct-nav? res) res (static-fn-code coerce-nav [res])))
  :else
  (static-val-code (coerce-nav o))))

(defn
 used-locals
 [locals-set form]
 (let
  [used-locals-cell (mutable-cell [])]
  (clojure.walk/postwalk
   (fn
    [e]
    (if
     (contains? locals-set e)
     (update-cell!
      used-locals-cell
      (fn* [p1__1140#] (conj p1__1140# e)))
     e))
   form)
  (get-cell used-locals-cell)))

(def ^{:dynamic true} *DEBUG-INLINE-CACHING* false)

(def ^{:dynamic true} *path-compile-files* false)

(defn
 mk-dynamic-path-maker
 [resolved-code ns-str used-locals-list possible-param]
 (let
  [code
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'clojure.core/fn)
      (clojure.core/list
       (clojure.core/vec
        (clojure.core/sequence
         (clojure.core/seq (clojure.core/concat used-locals-list)))))
      (clojure.core/list resolved-code))))
   ns
   (find-ns (symbol ns-str))]
  (when
   *DEBUG-INLINE-CACHING*
   (println "Produced code:")
   (clojure.pprint/pprint code)
   (println))
  (binding
   [*ns*
    ns
    *compile-files*
    (if *path-compile-files* *compile-files* false)]
   (eval+ code))))

(defn
 magic-precompilation
 [path ns-str used-locals-list possible-params]
 (let
  [magic-path (-> path magic-precompilation* static-combine)]
  (when
   *DEBUG-INLINE-CACHING*
   (println "Inline caching debug information")
   (println "--------------------------------")
   (println "Input path:" path "\n")
   (println "Processed path:" magic-path "\n"))
  (if
   (rich-nav? magic-path)
   (do
    (when *DEBUG-INLINE-CACHING* (println "Static result:" magic-path))
    (->CachedPathInfo false magic-path))
   (let
    [maker
     (mk-dynamic-path-maker
      (resolve-nav-code (->DynamicPath magic-path) possible-params)
      ns-str
      used-locals-list
      possible-params)]
    (->CachedPathInfo true maker)))))

(defn
 compiled-setval*
 [path val structure]
 (compiled-transform* path (fast-constantly val) structure))

(defn
 compiled-replace-in*
 [path
  transform-fn
  structure
  &
  {:keys [merge-fn], :or {merge-fn concat}}]
 (let
  [state (mutable-cell nil)]
  [(compiled-transform*
    path
    (fn
     [& args]
     (let
      [res (apply transform-fn args)]
      (if
       res
       (let
        [[ret user-ret] res]
        (->> user-ret (merge-fn (get-cell state)) (set-cell! state))
        ret)
       (last args))))
    structure)
   (get-cell state)]))

(defn-
 multi-transform-error-fn
 [& nav]
 (throw
  (ex-info
   "All navigation in multi-transform must end in 'terminal' navigators"
   {:nav nav})))

(defn
 compiled-multi-transform*
 [path structure]
 (compiled-transform* path multi-transform-error-fn structure))


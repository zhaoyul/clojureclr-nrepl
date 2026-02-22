(ns
 com.rpl.specter
 (:use
  [com.rpl.specter.protocols :only [ImplicitNav RichNavigator]]
  [com.rpl.specter.util-macros :only [doseqres]])
 (:require
  [com.rpl.specter.impl :as i]
  [com.rpl.specter.navs :as n]
  [clojure.walk :as cljwalk]
  [com.rpl.specter.macros :as macros]
  [clojure.set :as set]))

(defn-
 static-path?
 [path]
 (if
  (sequential? path)
  (every? static-path? path)
  (-> path com.rpl.specter.impl/dynamic-param? not)))

(defn
 wrap-dynamic-nav
 [f]
 (fn
  [& args]
  (let
   [ret (apply f args)]
   (cond
    (and (sequential? ret) (static-path? ret))
    (com.rpl.specter.impl/comp-paths* ret)
    (and (sequential? ret) (= 1 (count ret)))
    (first ret)
    :else
    ret))))

(do
 (defmacro
  defmacroalias
  [name target]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'do)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'def)
         (clojure.core/list name)
         (clojure.core/list
          (clojure.core/sequence
           (clojure.core/seq
            (clojure.core/concat
             (clojure.core/list 'var)
             (clojure.core/list target)))))))))
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'clojure.core/alter-meta!)
         (clojure.core/list
          (clojure.core/sequence
           (clojure.core/seq
            (clojure.core/concat
             (clojure.core/list 'var)
             (clojure.core/list name)))))
         (clojure.core/list 'clojure.core/merge)
         (clojure.core/list
          (clojure.core/apply
           clojure.core/array-map
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list :macro)
              (clojure.core/list true))))))))))))))
 (defmacroalias richnav com.rpl.specter.macros/richnav)
 (defmacroalias nav com.rpl.specter.macros/nav)
 (defmacroalias defnav com.rpl.specter.macros/defnav)
 (defmacroalias defrichnav com.rpl.specter.macros/defrichnav)
 (defmacro
  collector
  [params [_ [_ structure-sym] & body]]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter/richnav)
     (clojure.core/list params)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'select*)
         (clojure.core/list
          (clojure.core/vec
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list 'this__1092__auto__)
              (clojure.core/list 'vals__1093__auto__)
              (clojure.core/list structure-sym)
              (clojure.core/list 'next-fn__1094__auto__))))))
         (clojure.core/list
          (clojure.core/sequence
           (clojure.core/seq
            (clojure.core/concat
             (clojure.core/list 'next-fn__1094__auto__)
             (clojure.core/list
              (clojure.core/sequence
               (clojure.core/seq
                (clojure.core/concat
                 (clojure.core/list 'clojure.core/conj)
                 (clojure.core/list 'vals__1093__auto__)
                 (clojure.core/list
                  (clojure.core/sequence
                   (clojure.core/seq
                    (clojure.core/concat
                     (clojure.core/list 'do)
                     body))))))))
             (clojure.core/list structure-sym)))))))))
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'transform*)
         (clojure.core/list
          (clojure.core/vec
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list 'this__1092__auto__)
              (clojure.core/list 'vals__1093__auto__)
              (clojure.core/list structure-sym)
              (clojure.core/list 'next-fn__1094__auto__))))))
         (clojure.core/list
          (clojure.core/sequence
           (clojure.core/seq
            (clojure.core/concat
             (clojure.core/list 'next-fn__1094__auto__)
             (clojure.core/list
              (clojure.core/sequence
               (clojure.core/seq
                (clojure.core/concat
                 (clojure.core/list 'clojure.core/conj)
                 (clojure.core/list 'vals__1093__auto__)
                 (clojure.core/list
                  (clojure.core/sequence
                   (clojure.core/seq
                    (clojure.core/concat
                     (clojure.core/list 'do)
                     body))))))))
             (clojure.core/list structure-sym)))))))))))))
 (defmacro
  defcollector
  [name & body]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'def)
     (clojure.core/list name)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/collector)
         body))))))))
 (defn-
  late-bound-operation
  [bindings builder-op impls]
  (let
   [bindings
    (partition 2 bindings)
    params
    (map first bindings)
    curr-params
    (map second bindings)]
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'clojure.core/let)
      (clojure.core/list
       (clojure.core/vec
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'builder__1095__auto__)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list builder-op)
               (clojure.core/list
                (clojure.core/vec
                 (clojure.core/sequence
                  (clojure.core/seq (clojure.core/concat params)))))
               impls))))
           (clojure.core/list 'curr-params__1096__auto__)
           (clojure.core/list
            (clojure.core/vec
             (clojure.core/sequence
              (clojure.core/seq
               (clojure.core/concat curr-params))))))))))
      (clojure.core/list
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'if)
          (clojure.core/list
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list 'clojure.core/every?)
              (clojure.core/list
               (clojure.core/sequence
                (clojure.core/seq
                 (clojure.core/concat
                  (clojure.core/list 'clojure.core/complement)
                  (clojure.core/list
                   'com.rpl.specter.impl/dynamic-param?)))))
              (clojure.core/list 'curr-params__1096__auto__)))))
          (clojure.core/list
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list 'clojure.core/apply)
              (clojure.core/list 'builder__1095__auto__)
              (clojure.core/list 'curr-params__1096__auto__)))))
          (clojure.core/list
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list
               'com.rpl.specter.impl/->DynamicFunction)
              (clojure.core/list 'builder__1095__auto__)
              (clojure.core/list 'curr-params__1096__auto__)
              (clojure.core/list nil))))))))))))))
 (defmacro
  late-bound-nav
  [bindings & impls]
  (late-bound-operation bindings 'com.rpl.specter/nav impls))
 (defmacro
  late-bound-collector
  [bindings impl]
  (late-bound-operation bindings 'com.rpl.specter/collector [impl]))
 (defmacro
  late-bound-richnav
  [bindings & impls]
  (late-bound-operation bindings 'com.rpl.specter/richnav impls))
 (defmacro
  with-inline-debug
  [& body]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'clojure.core/binding)
     (clojure.core/list
      (clojure.core/vec
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list
           'com.rpl.specter.impl/*DEBUG-INLINE-CACHING*)
          (clojure.core/list true))))))
     body))))
 (defmacro
  declarepath
  [name]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'def)
     (clojure.core/list name)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list
          'com.rpl.specter.impl/local-declarepath)))))))))
 (defmacro
  providepath
  [name apath]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/providepath*)
     (clojure.core/list name)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))))))
 (defmacro
  recursive-path
  [params self-sym path]
  (if
   (empty? params)
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'clojure.core/let)
      (clojure.core/list
       (clojure.core/vec
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list self-sym)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list
                'com.rpl.specter.impl/local-declarepath))))))))))
      (clojure.core/list
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'com.rpl.specter/providepath)
          (clojure.core/list self-sym)
          (clojure.core/list path)))))
      (clojure.core/list self-sym))))
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'com.rpl.specter.impl/direct-nav-obj)
      (clojure.core/list
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'clojure.core/fn)
          (clojure.core/list params)
          (clojure.core/list
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat
              (clojure.core/list 'clojure.core/let)
              (clojure.core/list
               (clojure.core/vec
                (clojure.core/sequence
                 (clojure.core/seq
                  (clojure.core/concat
                   (clojure.core/list self-sym)
                   (clojure.core/list
                    (clojure.core/sequence
                     (clojure.core/seq
                      (clojure.core/concat
                       (clojure.core/list
                        'com.rpl.specter.impl/local-declarepath))))))))))
              (clojure.core/list
               (clojure.core/sequence
                (clojure.core/seq
                 (clojure.core/concat
                  (clojure.core/list 'com.rpl.specter/providepath)
                  (clojure.core/list self-sym)
                  (clojure.core/list path)))))
              (clojure.core/list self-sym))))))))))))))
 (def
  ^{:private true} sigs
  (fn
   [fdecl]
   (let
    [asig
     (fn
      [fdecl]
      (let
       [arglist
        (first fdecl)
        arglist
        (if
         (= '&form (first arglist))
         (subvec arglist 2 (count arglist))
         arglist)
        body
        (next fdecl)]
       (if
        (map? (first body))
        (if
         (next body)
         (with-meta
          arglist
          (conj (if (meta arglist) (meta arglist) {}) (first body)))
         arglist)
        arglist)))]
    (if
     (seq? (first fdecl))
     (loop
      [ret [] fdecls fdecl]
      (if
       fdecls
       (recur (conj ret (asig (first fdecls))) (next fdecls))
       (seq ret)))
     (list (asig fdecl))))))
 (defn-
  name-with-attributes
  "To be used in macro definitions.\n       Handles optional docstrings and attribute maps for a name to be defined\n       in a list of macro arguments. If the first macro argument is a string,\n       it is added as a docstring to name and removed from the macro argument\n       list. If afterwards the first macro argument is a map, its entries are\n       added to the name's metadata map and the map is removed from the\n       macro argument list. The return value is a vector containing the name\n       with its extended metadata map and the list of unprocessed macro\n       arguments."
  [name fdecl]
  (let
   [m
    (if (string? (first fdecl)) {:doc (first fdecl)} {})
    fdecl
    (if (string? (first fdecl)) (next fdecl) fdecl)
    m
    (if (map? (first fdecl)) (conj m (first fdecl)) m)
    fdecl
    (if (map? (first fdecl)) (next fdecl) fdecl)
    fdecl
    (if (vector? (first fdecl)) (list fdecl) fdecl)
    m
    (if (map? (last fdecl)) (conj m (last fdecl)) m)
    fdecl
    (if (map? (last fdecl)) (butlast fdecl) fdecl)
    m
    (conj {:arglists (list 'quote (sigs fdecl))} m)]
   [(with-meta name m) fdecl]))
 (defmacro
  dynamicnav
  [& args]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'clojure.core/vary-meta)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/wrap-dynamic-nav)
         (clojure.core/list
          (clojure.core/sequence
           (clojure.core/seq
            (clojure.core/concat
             (clojure.core/list 'clojure.core/fn)
             args))))))))
     (clojure.core/list 'clojure.core/assoc)
     (clojure.core/list :dynamicnav)
     (clojure.core/list true)))))
 (defmacro
  defdynamicnav
  "Defines a function that can choose what navigator to use at runtime based on\n        the dynamic context. The arguments will either be static values or\n        objects satisfying `dynamic-param?`. Use `late-bound-nav` to produce a runtime\n        navigator that uses the values of the dynamic params. See `selected?` for\n        an illustrative example of dynamic navs."
  [name & args]
  (let
   [[name args] (name-with-attributes name args)]
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'def)
      (clojure.core/list name)
      (clojure.core/list
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'com.rpl.specter/dynamicnav)
          args)))))))))
 (defn-
  ic-prepare-path
  [locals-set path]
  (cond
   (vector? path)
   (mapv (fn* [p1__1097#] (ic-prepare-path locals-set p1__1097#)) path)
   (symbol? path)
   (if
    (contains? locals-set path)
    (let
     [s
      (get locals-set path)
      embed
      (com.rpl.specter.impl/maybe-direct-nav
       path
       (-> s meta :direct-nav))]
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'com.rpl.specter.impl/->LocalSym)
        (clojure.core/list path)
        (clojure.core/list
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'quote)
            (clojure.core/list embed)))))))))
    (clojure.core/sequence
     (clojure.core/seq
      (clojure.core/concat
       (clojure.core/list 'com.rpl.specter.impl/->VarUse)
       (clojure.core/list path)
       (clojure.core/list
        (if-not
         (instance? System.Type (resolve path))
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'var)
            (clojure.core/list path))))))
       (clojure.core/list
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'quote)
           (clojure.core/list path)))))))))
   (com.rpl.specter.impl/fn-invocation? path)
   (let
    [[op & params] path]
    (if
     (or (= 'fn op) (special-symbol? op))
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'com.rpl.specter.impl/->SpecialFormUse)
        (clojure.core/list path)
        (clojure.core/list
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'quote)
            (clojure.core/list path))))))))
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'com.rpl.specter.impl/->FnInvocation)
        (clojure.core/list (ic-prepare-path locals-set op))
        (clojure.core/list
         (mapv
          (fn* [p1__1098#] (ic-prepare-path locals-set p1__1098#))
          params))
        (clojure.core/list
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'quote)
            (clojure.core/list path))))))))))
   :else
   (if
    (empty? (com.rpl.specter.impl/used-locals locals-set path))
    path
    (clojure.core/sequence
     (clojure.core/seq
      (clojure.core/concat
       (clojure.core/list 'com.rpl.specter.impl/->DynamicVal)
       (clojure.core/list
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'quote)
           (clojure.core/list path)))))))))))
 (defn-
  ic-possible-params
  [path]
  (do
   (mapcat
    (fn
     [e]
     (cond
      (or
       (set? e)
       (map? e)
       (symbol? e)
       (and
        (com.rpl.specter.impl/fn-invocation? e)
        (or
         (contains? #{'fn* 'fn} (first e))
         (special-symbol? (first e)))))
      [e]
      (sequential? e)
      (concat (if (vector? e) [e]) (ic-possible-params e))))
    path)))
 (defn-
  cljs-macroexpand
  [env form]
  (let
   [expand-fn
    (com.rpl.specter.impl/cljs-analyzer-macroexpand-1)
    mform
    (expand-fn env form)]
   (cond
    (identical? form mform)
    mform
    (and (seq? mform) (#{'js*} (first mform)))
    form
    :else
    (cljs-macroexpand env mform))))
 (defn-
  cljs-macroexpand-all*
  [env form]
  (if
   (and (seq? form) (#{'fn* 'cljs.core/fn 'fn} (first form)))
   form
   (let
    [expanded (if (seq? form) (cljs-macroexpand env form) form)]
    (clojure.walk/walk
     (fn* [p1__1099#] (cljs-macroexpand-all* env p1__1099#))
     identity
     expanded))))
 (defn-
  cljs-macroexpand-all
  [env form]
  (let [ret (cljs-macroexpand-all* env form)] ret))
 (defmacro
  path
  "Same as calling comp-paths, except it caches the composition of the static parts\n       of the path for later re-use (when possible). For almost all idiomatic uses\n       of Specter provides huge speedup. This macro is automatically used by the\n       select/transform/setval/replace-in/etc. macros."
  [& path]
  (let
   [platform
    (if (contains? &env :locals) :cljs :clj)
    local-syms
    (if
     (= platform :cljs)
     (-> &env :locals keys set)
     (-> &env keys set))
    used-locals
    (com.rpl.specter.impl/used-locals local-syms path)
    expanded
    (if
     (= platform :clj)
     (com.rpl.specter.impl/clj-macroexpand-all (vec path))
     (cljs-macroexpand-all &env (vec path)))
    prepared-path
    (ic-prepare-path local-syms expanded)
    possible-params
    (vec (ic-possible-params expanded))
    cache-sym
    (vary-meta
     (gensym "pathcache")
     merge
     {:cljs.analyzer/no-resolve true, :no-doc true, :private true})
    info-sym
    (gensym "info")
    get-cache-code
    (if
     (= platform :clj)
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'try)
        (clojure.core/list
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'com.rpl.specter.impl/get-cell)
            (clojure.core/list cache-sym)))))
        (clojure.core/list
         (clojure.core/sequence
          (clojure.core/seq
           (clojure.core/concat
            (clojure.core/list 'catch)
            (clojure.core/list 'System.InvalidCastException)
            (clojure.core/list 'e__1100__auto__)
            (clojure.core/list
             (clojure.core/sequence
              (clojure.core/seq
               (clojure.core/concat
                (clojure.core/list 'if)
                (clojure.core/list
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'clojure.core/bound?)
                    (clojure.core/list
                     (clojure.core/sequence
                      (clojure.core/seq
                       (clojure.core/concat
                        (clojure.core/list 'var)
                        (clojure.core/list cache-sym)))))))))
                (clojure.core/list
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'com.rpl.specter.impl/get-cell)
                    (clojure.core/list cache-sym)))))
                (clojure.core/list
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'do)
                    (clojure.core/list
                     (clojure.core/sequence
                      (clojure.core/seq
                       (clojure.core/concat
                        (clojure.core/list
                         'clojure.core/alter-var-root)
                        (clojure.core/list
                         (clojure.core/sequence
                          (clojure.core/seq
                           (clojure.core/concat
                            (clojure.core/list 'var)
                            (clojure.core/list cache-sym)))))
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
                                 (clojure.core/list
                                  '___1101__auto__))))))
                            (clojure.core/list
                             (clojure.core/sequence
                              (clojure.core/seq
                               (clojure.core/concat
                                (clojure.core/list
                                 'com.rpl.specter.impl/mutable-cell)))))))))))))
                    (clojure.core/list nil))))))))))))))))
     cache-sym)
    add-cache-code
    (if
     (= platform :clj)
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'com.rpl.specter.impl/set-cell!)
        (clojure.core/list cache-sym)
        (clojure.core/list info-sym))))
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list 'def)
        (clojure.core/list cache-sym)
        (clojure.core/list info-sym)))))
    precompiled-sym
    (gensym "precompiled")
    handle-params-code
    (if
     (= platform :clj)
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list precompiled-sym)
        used-locals)))
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list precompiled-sym)
        (clojure.core/list possible-params)))))]
   (if
    (= platform :clj)
    (com.rpl.specter.impl/intern*
     *ns*
     cache-sym
     (com.rpl.specter.impl/mutable-cell)))
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'clojure.core/let)
      (clojure.core/list
       (clojure.core/vec
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'info__1102__auto__)
           (clojure.core/list get-cache-code)
           (clojure.core/list 'info__1102__auto__)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list 'if)
               (clojure.core/list
                (clojure.core/sequence
                 (clojure.core/seq
                  (clojure.core/concat
                   (clojure.core/list 'clojure.core/nil?)
                   (clojure.core/list 'info__1102__auto__)))))
               (clojure.core/list
                (clojure.core/sequence
                 (clojure.core/seq
                  (clojure.core/concat
                   (clojure.core/list 'clojure.core/let)
                   (clojure.core/list
                    (clojure.core/vec
                     (clojure.core/sequence
                      (clojure.core/seq
                       (clojure.core/concat
                        (clojure.core/list info-sym)
                        (clojure.core/list
                         (clojure.core/sequence
                          (clojure.core/seq
                           (clojure.core/concat
                            (clojure.core/list
                             'com.rpl.specter.impl/magic-precompilation)
                            (clojure.core/list prepared-path)
                            (clojure.core/list (str *ns*))
                            (clojure.core/list
                             (clojure.core/sequence
                              (clojure.core/seq
                               (clojure.core/concat
                                (clojure.core/list 'quote)
                                (clojure.core/list used-locals)))))
                            (clojure.core/list
                             (clojure.core/sequence
                              (clojure.core/seq
                               (clojure.core/concat
                                (clojure.core/list 'quote)
                                (clojure.core/list
                                 possible-params))))))))))))))
                   (clojure.core/list add-cache-code)
                   (clojure.core/list info-sym)))))
               (clojure.core/list 'info__1102__auto__)))))
           (clojure.core/list precompiled-sym)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list
                'com.rpl.specter.impl/cached-path-info-precompiled)
               (clojure.core/list 'info__1102__auto__)))))
           (clojure.core/list 'dynamic?__1103__auto__)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list
                'com.rpl.specter.impl/cached-path-info-dynamic?)
               (clojure.core/list 'info__1102__auto__))))))))))
      (clojure.core/list
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'if)
          (clojure.core/list 'dynamic?__1103__auto__)
          (clojure.core/list handle-params-code)
          (clojure.core/list precompiled-sym))))))))))
 (defmacro
  select
  "Navigates to and returns a sequence of all the elements specified by the path.\n       This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-select*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  select-one!
  "Returns exactly one element, throws exception if zero or multiple elements found.\n        This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-select-one!*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  select-one
  "Like select, but returns either one element or nil. Throws exception if multiple elements found.\n        This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-select-one*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  select-first
  "Returns first element found.\n        This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-select-first*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  select-any
  "Returns any element found or [[NONE]] if nothing selected. This is the most\n       efficient of the various selection operations.\n       This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-select-any*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  selected-any?
  "Returns true if any element was selected, false otherwise.\n       This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-selected-any?*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  transform
  "Navigates to each value specified by the path and replaces it by the result of running\n       the transform-fn on it.\n       This macro will do inline caching of the path."
  [apath transform-fn structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-transform*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list transform-fn)
     (clojure.core/list structure)))))
 (defmacro
  vtransform
  "Navigates to each value specified by the path and replaces it by the result of running\n       the transform-fn on two arguments: the collected values as a vector, and the navigated value."
  [apath transform-fn structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-vtransform*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list transform-fn)
     (clojure.core/list structure)))))
 (defmacro
  multi-transform
  "Just like `transform` but expects transform functions to be specified\n       inline in the path using `terminal` or `vterminal`. Error is thrown if navigation finishes\n       at a non-terminal navigator. `terminal-val` is a wrapper around `terminal` and is\n       the `multi-transform` equivalent of `setval`.\n       This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list
      'com.rpl.specter.impl/compiled-multi-transform*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  setval
  "Navigates to each value specified by the path and replaces it by `aval`.\n       This macro will do inline caching of the path."
  [apath aval structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-setval*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list aval)
     (clojure.core/list structure)))))
 (defmacro
  traverse
  "Return a reducible object that traverses over `structure` to every element\n       specified by the path.\n       This macro will do inline caching of the path."
  [apath structure]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/do-compiled-traverse)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list structure)))))
 (defmacro
  traverse-all
  "Returns a transducer that traverses over each element with the given path."
  [apath]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-traverse-all*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))))))
 (defmacro
  replace-in
  "Similar to transform, except returns a pair of [transformed-structure sequence-of-user-ret].\n       The transform-fn in this case is expected to return [ret user-ret]. ret is\n       what's used to transform the data structure, while user-ret will be added to the user-ret sequence\n       in the final return. replace-in is useful for situations where you need to know the specific values\n       of what was transformed in the data structure.\n       This macro will do inline caching of the path."
  [apath transform-fn structure & args]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/compiled-replace-in*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'com.rpl.specter/path)
         (clojure.core/list apath)))))
     (clojure.core/list transform-fn)
     (clojure.core/list structure)
     args))))
 (defmacro
  collected?
  "Creates a filter function navigator that takes in all the collected values\n       as input. For arguments, can use `(collected? [a b] ...)` syntax to look\n       at each collected value as individual arguments, or `(collected? v ...)` syntax\n       to capture all the collected values as a single vector."
  [params & body]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.impl/collected?*)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'fn)
         (clojure.core/list
          (clojure.core/vec
           (clojure.core/sequence
            (clojure.core/seq
             (clojure.core/concat (clojure.core/list params))))))
         body))))))))
 (defn- protpath-sym [name] (-> name (str "-prot") symbol))
 (defn- protpath-meth-sym [name] (-> name (str "-retrieve") symbol))
 (defmacro
  defprotocolpath
  "Defines a navigator that chooses the path to take based on the type\n       of the value at the current point. May be specified with parameters to\n       specify that all extensions must require that number of parameters.\n\n       Currently not available for ClojureScript.\n\n       Example of usage:\n       (defrecord SingleAccount [funds])\n       (defrecord FamilyAccount [single-accounts])\n\n       (defprotocolpath FundsPath)\n       (extend-protocolpath FundsPath\n         SingleAccount :funds\n         FamilyAccount [ALL FundsPath]\n         )\n     "
  ([name]
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'com.rpl.specter/defprotocolpath)
      (clojure.core/list name)
      (clojure.core/list
       (clojure.core/vec
        (clojure.core/sequence
         (clojure.core/seq (clojure.core/concat)))))))))
  ([name params]
   (let
    [prot-name
     (protpath-sym name)
     m
     (protpath-meth-sym name)
     num-params
     (count params)
     ssym
     (gensym "structure")
     rargs
     [(gensym "vals") ssym (gensym "next-fn")]
     retrieve
     (clojure.core/sequence
      (clojure.core/seq
       (clojure.core/concat
        (clojure.core/list m)
        (clojure.core/list ssym)
        params)))]
    (clojure.core/sequence
     (clojure.core/seq
      (clojure.core/concat
       (clojure.core/list 'do)
       (clojure.core/list
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'clojure.core/defprotocol)
           (clojure.core/list prot-name)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list m)
               (clojure.core/list
                (clojure.core/vec
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'structure__1104__auto__)
                    params)))))))))))))
       (clojure.core/list
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'com.rpl.specter/defrichnav)
           (clojure.core/list name)
           (clojure.core/list params)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list 'select*)
               (clojure.core/list
                (clojure.core/vec
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'this__1105__auto__)
                    rargs)))))
               (clojure.core/list
                (clojure.core/sequence
                 (clojure.core/seq
                  (clojure.core/concat
                   (clojure.core/list 'clojure.core/let)
                   (clojure.core/list
                    (clojure.core/vec
                     (clojure.core/sequence
                      (clojure.core/seq
                       (clojure.core/concat
                        (clojure.core/list 'inav__1106__auto__)
                        (clojure.core/list retrieve))))))
                   (clojure.core/list
                    (clojure.core/sequence
                     (clojure.core/seq
                      (clojure.core/concat
                       (clojure.core/list
                        'com.rpl.specter.impl/exec-select*)
                       (clojure.core/list 'inav__1106__auto__)
                       rargs))))))))))))
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list 'transform*)
               (clojure.core/list
                (clojure.core/vec
                 (clojure.core/sequence
                  (clojure.core/seq
                   (clojure.core/concat
                    (clojure.core/list 'this__1105__auto__)
                    rargs)))))
               (clojure.core/list
                (clojure.core/sequence
                 (clojure.core/seq
                  (clojure.core/concat
                   (clojure.core/list 'clojure.core/let)
                   (clojure.core/list
                    (clojure.core/vec
                     (clojure.core/sequence
                      (clojure.core/seq
                       (clojure.core/concat
                        (clojure.core/list 'inav__1106__auto__)
                        (clojure.core/list retrieve))))))
                   (clojure.core/list
                    (clojure.core/sequence
                     (clojure.core/seq
                      (clojure.core/concat
                       (clojure.core/list
                        'com.rpl.specter.impl/exec-transform*)
                       (clojure.core/list 'inav__1106__auto__)
                       rargs))))))))))))))))))))))
 (defmacro
  satisfies-protpath?
  [protpath o]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'clojure.core/satisfies?)
     (clojure.core/list (protpath-sym protpath))
     (clojure.core/list o)))))
 (defn
  extend-protocolpath*
  [protpath-prot extensions]
  (let
   [m
    (-> protpath-prot :sigs keys first)
    params
    (-> protpath-prot :sigs first last :arglists first)]
   (doseq
    [[atype path-code] extensions]
    (extend
     atype
     protpath-prot
     {m
      (binding
       [*compile-files* false]
       (eval
        (clojure.core/sequence
         (clojure.core/seq
          (clojure.core/concat
           (clojure.core/list 'clojure.core/fn)
           (clojure.core/list params)
           (clojure.core/list
            (clojure.core/sequence
             (clojure.core/seq
              (clojure.core/concat
               (clojure.core/list 'com.rpl.specter/path)
               (clojure.core/list path-code))))))))))}))))
 (defmacro
  extend-protocolpath
  "Used in conjunction with `defprotocolpath`. See [[defprotocolpath]]."
  [protpath & extensions]
  (let
   [extensions
    (partition 2 extensions)
    embed
    (vec
     (for
      [[t p] extensions]
      [t
       (clojure.core/sequence
        (clojure.core/seq
         (clojure.core/concat
          (clojure.core/list 'quote)
          (clojure.core/list p))))]))]
   (clojure.core/sequence
    (clojure.core/seq
     (clojure.core/concat
      (clojure.core/list 'com.rpl.specter/extend-protocolpath*)
      (clojure.core/list (protpath-sym protpath))
      (clojure.core/list embed))))))
 (defmacro
  end-fn
  [& args]
  (clojure.core/sequence
   (clojure.core/seq
    (clojure.core/concat
     (clojure.core/list 'com.rpl.specter.navs/->SrangeEndFunction)
     (clojure.core/list
      (clojure.core/sequence
       (clojure.core/seq
        (clojure.core/concat
         (clojure.core/list 'clojure.core/fn)
         args)))))))))

(defn
 comp-paths
 "Returns a compiled version of the given path for use with\n   compiled-{select/transform/setval/etc.} functions."
 [& apath]
 (com.rpl.specter.impl/comp-paths* (vec apath)))

(def
 ^{:doc "Version of select that takes in a path precompiled with comp-paths"} compiled-select
 com.rpl.specter.impl/compiled-select*)

(defn
 select*
 "Navigates to and returns a sequence of all the elements specified by the path."
 [path structure]
 (compiled-select (com.rpl.specter.impl/comp-paths* path) structure))

(def
 ^{:doc "Version of select-one that takes in a path precompiled with comp-paths"} compiled-select-one
 com.rpl.specter.impl/compiled-select-one*)

(defn
 select-one*
 "Like select, but returns either one element or nil. Throws exception if multiple elements found"
 [path structure]
 (compiled-select-one
  (com.rpl.specter.impl/comp-paths* path)
  structure))

(def
 ^{:doc "Version of select-one! that takes in a path precompiled with comp-paths"} compiled-select-one!
 com.rpl.specter.impl/compiled-select-one!*)

(defn
 select-one!*
 "Returns exactly one element, throws exception if zero or multiple elements found"
 [path structure]
 (compiled-select-one!
  (com.rpl.specter.impl/comp-paths* path)
  structure))

(def
 ^{:doc "Version of select-first that takes in a path precompiled with comp-paths"} compiled-select-first
 com.rpl.specter.impl/compiled-select-first*)

(defn
 select-first*
 "Returns first element found."
 [path structure]
 (compiled-select-first
  (com.rpl.specter.impl/comp-paths* path)
  structure))

(def
 ^{:doc "Version of select-any that takes in a path precompiled with comp-paths"} compiled-select-any
 com.rpl.specter.impl/compiled-select-any*)

(def
 ^{:doc "Global value used to indicate no elements selected during\n             [[select-any]]."} NONE
 com.rpl.specter.impl/NONE)

(defn
 select-any*
 "Returns any element found or [[NONE]] if nothing selected. This is the most\n   efficient of the various selection operations."
 [path structure]
 (compiled-select-any
  (com.rpl.specter.impl/comp-paths* path)
  structure))

(def
 ^{:doc "Version of selected-any? that takes in a path precompiled with comp-paths"} compiled-selected-any?
 com.rpl.specter.impl/compiled-selected-any?*)

(defn
 selected-any?*
 "Returns true if any element was selected, false otherwise."
 [path structure]
 (compiled-selected-any?
  (com.rpl.specter.impl/comp-paths* path)
  structure))

(def
 ^{:doc "Version of traverse that takes in a path precompiled with comp-paths"} compiled-traverse
 com.rpl.specter.impl/do-compiled-traverse)

(defn
 traverse*
 "Return a reducible object that traverses over `structure` to every element\n   specified by the path"
 [apath structure]
 (compiled-traverse (com.rpl.specter.impl/comp-paths* apath) structure))

(def
 ^{:doc "Version of traverse-all that takes in a path precompiled with comp-paths"} compiled-traverse-all
 com.rpl.specter.impl/compiled-traverse-all*)

(defn
 traverse-all*
 "Returns a transducer that traverses over each element with the given path."
 [apath]
 (compiled-traverse-all (com.rpl.specter.impl/comp-paths* apath)))

(def
 ^{:doc "Version of transform that takes in a path precompiled with comp-paths"} compiled-transform
 com.rpl.specter.impl/compiled-transform*)

(def
 ^{:doc "Version of vtransform that takes in a path precompiled with comp-paths"} compiled-vtransform
 com.rpl.specter.impl/compiled-vtransform*)

(defn
 transform*
 "Navigates to each value specified by the path and replaces it by the result of running\n  the transform-fn on it"
 [path transform-fn structure]
 (compiled-transform
  (com.rpl.specter.impl/comp-paths* path)
  transform-fn
  structure))

(def
 ^{:doc "Version of `multi-transform` that takes in a path precompiled with `comp-paths`"} compiled-multi-transform
 com.rpl.specter.impl/compiled-multi-transform*)

(defn
 multi-transform*
 "Just like `transform` but expects transform functions to be specified\n   inline in the path using `terminal` or `vterminal`. Error is thrown if navigation finishes\n   at a non-terminal navigator. `terminal-val` is a wrapper around `terminal` and is\n   the `multi-transform` equivalent of `setval`."
 [path structure]
 (compiled-multi-transform
  (com.rpl.specter.impl/comp-paths* path)
  structure))

(def
 ^{:doc "Version of setval that takes in a path precompiled with comp-paths"} compiled-setval
 com.rpl.specter.impl/compiled-setval*)

(defn
 setval*
 "Navigates to each value specified by the path and replaces it by val"
 [path val structure]
 (compiled-setval
  (com.rpl.specter.impl/comp-paths* path)
  val
  structure))

(def
 ^{:doc "Version of replace-in that takes in a path precompiled with comp-paths"} compiled-replace-in
 com.rpl.specter.impl/compiled-replace-in*)

(defn
 replace-in*
 "Similar to transform, except returns a pair of [transformed-structure sequence-of-user-ret].\n   The transform-fn in this case is expected to return [ret user-ret]. ret is\n   what's used to transform the data structure, while user-ret will be added to the user-ret sequence\n   in the final return. replace-in is useful for situations where you need to know the specific values\n   of what was transformed in the data structure."
 [path
  transform-fn
  structure
  &
  {:keys [merge-fn], :or {merge-fn concat}}]
 (compiled-replace-in
  (com.rpl.specter.impl/comp-paths* path)
  transform-fn
  structure
  :merge-fn
  merge-fn))

(def late-path com.rpl.specter.impl/late-path)

(def dynamic-param? com.rpl.specter.impl/dynamic-param?)

(def late-resolved-fn com.rpl.specter.impl/late-resolved-fn)

(defdynamicnav
 ^{:doc "Turns a navigator that takes one argument into a navigator that takes\n          many arguments and uses the same navigator with each argument. There\n          is no performance cost to using this. See implementation of `keypath`"} eachnav
 [navfn]
 (let
  [latenavfn (late-resolved-fn navfn)]
  (dynamicnav [& args] (map latenavfn args))))

(def local-declarepath com.rpl.specter.impl/local-declarepath)

(defnav
 ^{:doc "Stops navigation at this point. For selection returns nothing and for\n          transformation returns the structure unchanged"} STOP
 []
 (select* [this structure next-fn] NONE)
 (transform* [this structure next-fn] structure))

(def
 ^{:doc "Stays navigated at the current point. Essentially a no-op navigator."} STAY
 com.rpl.specter.impl/STAY*)

(def
 ^{:doc "Defines an endpoint in the navigation the transform function run. The transform\n          function works just like it does in `transform`, with collected values\n          given as the first arguments"} terminal
 (richnav
  [afn]
  (select* [this vals structure next-fn] NONE)
  (transform*
   [this vals structure next-fn]
   (com.rpl.specter.impl/terminal* afn vals structure))))

(def
 ^{:doc "Defines an endpoint in the navigation the transform function run.The transform\n          function works differently than it does in `transform`. Rather than receive\n          collected vals spliced in as the first arguments to the function, this function\n          always takes two arguemnts. The first is all collected vals in a vector, and\n          the second is the navigated value."} vterminal
 (richnav
  [afn]
  (select* [this vals structure next-fn] NONE)
  (transform* [this vals structure next-fn] (afn vals structure))))

(defn
 ^{:direct-nav true} terminal-val
 "Like `terminal` but specifies a val to set at the location regardless of\n   the collected values or the value at the location."
 [v]
 (terminal (com.rpl.specter.impl/fast-constantly v)))

(defnav
 ^{:doc "Navigate to every element of the collection. For maps navigates to\n          a vector of `[key value]`."} ALL
 []
 (select*
  [this structure next-fn]
  (com.rpl.specter.navs/all-select structure next-fn))
 (transform*
  [this structure next-fn]
  (com.rpl.specter.navs/all-transform structure next-fn)))

(defnav
 ^{:doc "Same as ALL, except maintains metadata on the structure."} ALL-WITH-META
 []
 (select*
  [this structure next-fn]
  (com.rpl.specter.navs/all-select structure next-fn))
 (transform*
  [this structure next-fn]
  (let
   [m
    (meta structure)
    res
    (com.rpl.specter.navs/all-transform structure next-fn)]
   (if (some? res) (with-meta res m)))))

(defnav
 ^{:doc "Navigate to each value of the map. This is more efficient than\n          navigating via [ALL LAST]"} MAP-VALS
 []
 (select*
  [this structure next-fn]
  (doseqres NONE [v (vals structure)] (next-fn v)))
 (transform*
  [this structure next-fn]
  (com.rpl.specter.navs/map-vals-transform structure next-fn)))

(defnav
 ^{:doc "Navigate to each key of the map. This is more efficient than\n          navigating via [ALL FIRST]"} MAP-KEYS
 []
 (select*
  [this structure next-fn]
  (doseqres NONE [k (keys structure)] (next-fn k)))
 (transform*
  [this structure next-fn]
  (com.rpl.specter.navs/map-keys-transform structure next-fn)))

(defcollector VAL [] (collect-val [this structure] structure))

(def
 ^{:doc "Navigate to the last element of the collection. If the collection is\n          empty navigation is stopped at this point."} LAST
 (com.rpl.specter.navs/PosNavigator
  com.rpl.specter.navs/get-last
  com.rpl.specter.navs/update-last))

(def
 ^{:doc "Navigate to the first element of the collection. If the collection is\n          empty navigation is stopped at this point."} FIRST
 (com.rpl.specter.navs/PosNavigator
  com.rpl.specter.navs/get-first
  com.rpl.specter.navs/update-first))

(defnav
 ^{:doc "Uses start-index-fn and end-index-fn to determine the bounds of the subsequence\n          to select when navigating. `start-index-fn` takes in the structure as input. `end-index-fn`\n          can be one of two forms. If a regular function (e.g. defined with `fn`), it takes in only the structure as input. If a function defined using special `end-fn` macro, it takes in the structure and the result of `start-index-fn`."} srange-dynamic
 [start-index-fn end-index-fn]
 (select*
  [this structure next-fn]
  (let
   [s (start-index-fn structure)]
   (com.rpl.specter.navs/srange-select
    structure
    s
    (com.rpl.specter.navs/invoke-end-fn end-index-fn structure s)
    next-fn)))
 (transform*
  [this structure next-fn]
  (let
   [s (start-index-fn structure)]
   (com.rpl.specter.navs/srange-transform
    structure
    s
    (com.rpl.specter.navs/invoke-end-fn end-index-fn structure s)
    next-fn))))

(defnav
 ^{:doc "Navigates to the subsequence bound by the indexes start (inclusive)\n          and end (exclusive)"} srange
 [start end]
 (select*
  [this structure next-fn]
  (com.rpl.specter.navs/srange-select structure start end next-fn))
 (transform*
  [this structure next-fn]
  (com.rpl.specter.navs/srange-transform structure start end next-fn)))

(defnav
 ^{:doc "Navigates to every continuous subsequence of elements matching `pred`"} continuous-subseqs
 [pred]
 (select*
  [this structure next-fn]
  (doseqres
   NONE
   [[s e] (com.rpl.specter.impl/matching-ranges structure pred)]
   (com.rpl.specter.navs/srange-select structure s e next-fn)))
 (transform*
  [this structure next-fn]
  (com.rpl.specter.impl/continuous-subseqs-transform*
   pred
   structure
   next-fn)))

(defnav
 ^{:doc "Navigate to the empty subsequence before the first element of the collection."} BEGINNING
 []
 (select*
  [this structure next-fn]
  (next-fn (if (string? structure) "" [])))
 (transform*
  [this structure next-fn]
  (if
   (string? structure)
   (str (next-fn "") structure)
   (let
    [to-prepend (next-fn [])]
    (com.rpl.specter.navs/prepend-all structure to-prepend)))))

(defnav
 ^{:doc "Navigate to the empty subsequence after the last element of the collection."} END
 []
 (select*
  [this structure next-fn]
  (next-fn (if (string? structure) "" [])))
 (transform*
  [this structure next-fn]
  (if
   (string? structure)
   (str structure (next-fn ""))
   (let
    [to-append (next-fn [])]
    (com.rpl.specter.navs/append-all structure to-append)))))

(defnav
 ^{:doc "Navigate to 'void' elem in the set.\n          For transformations - if result is not `NONE`,\n          then add that value to the set."} NONE-ELEM
 []
 (select* [this structure next-fn] (next-fn NONE))
 (transform*
  [this structure next-fn]
  (let
   [newe (next-fn NONE)]
   (if
    (identical? NONE newe)
    structure
    (if (nil? structure) #{newe} (conj structure newe))))))

(defnav
 ^{:doc "Navigate to 'void' element before the sequence.\n          For transformations – if result is not `NONE`,\n          then prepend that value."} BEFORE-ELEM
 []
 (select* [this structure next-fn] (next-fn NONE))
 (transform*
  [this structure next-fn]
  (let
   [newe (next-fn NONE)]
   (if
    (identical? NONE newe)
    structure
    (com.rpl.specter.navs/prepend-one structure newe)))))

(defnav
 ^{:doc "Navigate to 'void' element after the sequence.\n          For transformations – if result is not `NONE`,\n          then append that value."} AFTER-ELEM
 []
 (select* [this structure next-fn] (next-fn NONE))
 (transform*
  [this structure next-fn]
  (let
   [newe (next-fn NONE)]
   (if
    (identical? NONE newe)
    structure
    (com.rpl.specter.navs/append-one structure newe)))))

(defnav
 ^{:doc "Navigates to the specified subset (by taking an intersection).\n          In a transform, that subset in the original set is changed to the\n          new value of the subset."} subset
 [aset]
 (select*
  [this structure next-fn]
  (next-fn (clojure.set/intersection structure aset)))
 (transform*
  [this structure next-fn]
  (let
   [subset
    (clojure.set/intersection structure aset)
    newset
    (next-fn subset)]
   (->
    structure
    (clojure.set/difference subset)
    (clojure.set/union newset)))))

(defnav
 ^{:doc "Navigates to the specified submap (using select-keys).\n          In a transform, that submap in the original map is changed to the new\n          value of the submap."} submap
 [m-keys]
 (select*
  [this structure next-fn]
  (next-fn (select-keys structure m-keys)))
 (transform*
  [this structure next-fn]
  (let
   [submap (select-keys structure m-keys) newmap (next-fn submap)]
   (merge (reduce dissoc structure m-keys) newmap))))

(defdynamicnav
 subselect
 "Navigates to a sequence that contains the results of (select ...),\n  but is a view to the original structure that can be transformed.\n\n  Requires that the input navigators will walk the structure's\n  children in the same order when executed on \"select\" and then\n  \"transform\".\n\n  If transformed sequence is smaller than input sequence, missing entries\n  will be filled in with NONE, triggering removal if supported by that navigator.\n\n  Value collection (e.g. collect, collect-one) may not be used in the subpath."
 [& path]
 (late-bound-nav
  [late (late-path path)]
  (select*
   [this structure next-fn]
   (next-fn (compiled-select late structure)))
  (transform*
   [this structure next-fn]
   (let
    [select-result
     (compiled-select late structure)
     transformed
     (next-fn select-result)
     values-to-insert
     (com.rpl.specter.impl/mutable-cell (seq transformed))]
    (compiled-transform
     late
     (fn
      [_]
      (let
       [vs (com.rpl.specter.impl/get-cell values-to-insert)]
       (if
        vs
        (do
         (com.rpl.specter.impl/update-cell! values-to-insert next)
         (first vs))
        NONE)))
     structure)))))

(defrichnav
 ^{:doc "Navigates to the given key in the map (not to the value). Navigates only if the\n          key currently exists in the map. Can transform to NONE to remove the key/value\n          pair from the map."} map-key
 [key]
 (select*
  [this vals structure next-fn]
  (if (contains? structure key) (next-fn vals key) NONE))
 (transform*
  [this vals structure next-fn]
  (if
   (contains? structure key)
   (let
    [newkey (next-fn vals key) dissoced (dissoc structure key)]
    (if
     (identical? NONE newkey)
     dissoced
     (assoc dissoced newkey (get structure key))))
   structure)))

(defrichnav
 ^{:doc "Navigates to the given element in the set only if it exists in the set.\n          Can transform to NONE to remove the element from the set."} set-elem
 [elem]
 (select*
  [this vals structure next-fn]
  (if (contains? structure elem) (next-fn vals elem) NONE))
 (transform*
  [this vals structure next-fn]
  (if
   (contains? structure elem)
   (let
    [newelem (next-fn vals elem) removed (disj structure elem)]
    (if (identical? NONE newelem) removed (conj removed newelem)))
   structure)))

(def
 ^{:doc "Navigate to the specified keys one after another. If navigate to NONE,\n             that element is removed from the map or vector."} keypath
 (eachnav com.rpl.specter.navs/keypath*))

(def
 ^{:doc "Navigate to the specified keys one after another, only if they exist\n             in the data structure. If navigate to NONE, that element is removed\n             from the map or vector."} must
 (eachnav com.rpl.specter.navs/must*))

(def
 ^{:doc "Navigate to the specified indices one after another. If navigate to\n            NONE, that element is removed from the sequence."} nthpath
 (eachnav com.rpl.specter.navs/nthpath*))

(defrichnav
 ^{:doc "Navigates to the empty space between the index and the prior index. For select\n          navigates to NONE, and transforms to non-NONE insert at that position."} before-index
 [index]
 (select* [this vals structure next-fn] NONE)
 (transform*
  [this vals structure next-fn]
  (let
   [v (next-fn vals NONE)]
   (if
    (identical? NONE v)
    structure
    (com.rpl.specter.navs/insert-before-idx structure index v)))))

(defrichnav
 ^{:doc "Navigates to the index of the sequence if within 0 and size. Transforms move element\n          at that index to the new index, shifting other elements in the sequence."} index-nav
 [i]
 (select*
  [this vals structure next-fn]
  (if (and (>= i 0) (< i (count structure))) (next-fn vals i) NONE))
 (transform*
  [this vals structure next-fn]
  (if
   (and (>= i 0) (< i (count structure)))
   (let
    [newi (next-fn vals i)]
    (if
     (= newi i)
     structure
     (let
      [v (nth structure i)]
      (if
       (vector? structure)
       (let
        [shifted
         (if
          (< newi i)
          (loop
           [j (dec i) s structure]
           (if
            (< j newi)
            s
            (recur (dec j) (assoc s (inc j) (nth s j)))))
          (loop
           [j (inc i) s structure]
           (if
            (> j newi)
            s
            (recur (inc j) (assoc s (dec j) (nth s j))))))]
        (assoc shifted newi v))
       (->>
        structure
        (setval (nthpath i) NONE)
        (setval (before-index newi) v))))))
   structure)))

(defnav
 ^{:doc "Navigate to [index elem] pairs for each element in a sequence. The sequence will be indexed\n          starting from `start`. Changing index in transform has same effect as `index-nav`. Indices seen\n          during transform take into account any shifting from prior sequence elements changing indices."} indexed-vals
 [start]
 (select*
  [this structure next-fn]
  (let
   [i (com.rpl.specter.impl/mutable-cell (dec start))]
   (doseqres
    NONE
    [e structure]
    (com.rpl.specter.impl/update-cell! i inc)
    (next-fn [(com.rpl.specter.impl/get-cell i) e]))))
 (transform*
  [this structure next-fn]
  (let
   [indices
    (com.rpl.specter.impl/mutable-cell (-> structure count range))]
   (reduce
    (fn
     [s e]
     (let
      [curri
       (first (com.rpl.specter.impl/get-cell indices))
       [newi* newe]
       (next-fn [(+ start curri) e])
       newi
       (- newi* start)]
      (com.rpl.specter.impl/update-cell!
       indices
       (fn
        [ii]
        (let
         [ii2 (next ii)]
         (if
          (> newi curri)
          (transform
           [ALL
            (fn* [p1__1107#] (>= p1__1107# (inc curri)))
            (fn* [p1__1108#] (<= p1__1108# newi))]
           dec
           ii2)
          ii2))))
      (->>
       s
       (setval (nthpath curri) newe)
       (setval (index-nav curri) newi))))
    structure
    structure))))

(def
 ^{:doc "`indexed-vals` with a starting index of 0."} INDEXED-VALS
 (indexed-vals 0))

(defrichnav
 ^{:doc "Navigates to result of running `afn` on the currently navigated value."} view
 [afn]
 (select* [this vals structure next-fn] (next-fn vals (afn structure)))
 (transform*
  [this vals structure next-fn]
  (next-fn vals (afn structure))))

(defnav
 ^{:doc "Navigate to the result of running `parse-fn` on the value. For\n          transforms, the transformed value then has `unparse-fn` run on\n          it to get the final value at this point."} parser
 [parse-fn unparse-fn]
 (select* [this structure next-fn] (next-fn (parse-fn structure)))
 (transform*
  [this structure next-fn]
  (unparse-fn (next-fn (parse-fn structure)))))

(defnav
 ^{:doc "Navigates to atom value."} ATOM
 []
 (select* [this structure next-fn] (next-fn @structure))
 (transform*
  [this structure next-fn]
  (do (swap! structure next-fn) structure)))

(defnav
 regex-nav
 [re]
 (select*
  [this structure next-fn]
  (doseqres NONE [s (re-seq re structure)] (next-fn s)))
 (transform*
  [this structure next-fn]
  (clojure.string/replace structure re next-fn)))

(defdynamicnav
 selected?
 "Filters the current value based on whether a path finds anything.\n  e.g. (selected? :vals ALL even?) keeps the current element only if an\n  even number exists for the :vals key."
 [& path]
 (if-let
  [afn (com.rpl.specter.navs/extract-basic-filter-fn path)]
  afn
  (late-bound-richnav
   [late (late-path path)]
   (select*
    [this vals structure next-fn]
    (com.rpl.specter.impl/filter-select
     (fn*
      [p1__1109#]
      (com.rpl.specter.navs/selected?* late vals p1__1109#))
     vals
     structure
     next-fn))
   (transform*
    [this vals structure next-fn]
    (com.rpl.specter.impl/filter-transform
     (fn*
      [p1__1110#]
      (com.rpl.specter.navs/selected?* late vals p1__1110#))
     vals
     structure
     next-fn)))))

(defdynamicnav
 not-selected?
 [& path]
 (if-let
  [afn (com.rpl.specter.navs/extract-basic-filter-fn path)]
  (fn [s] (not (afn s)))
  (late-bound-richnav
   [late (late-path path)]
   (select*
    [this vals structure next-fn]
    (com.rpl.specter.impl/filter-select
     (fn*
      [p1__1111#]
      (com.rpl.specter.navs/not-selected?* late vals p1__1111#))
     vals
     structure
     next-fn))
   (transform*
    [this vals structure next-fn]
    (com.rpl.specter.impl/filter-transform
     (fn*
      [p1__1112#]
      (com.rpl.specter.navs/not-selected?* late vals p1__1112#))
     vals
     structure
     next-fn)))))

(defdynamicnav
 filterer
 "Navigates to a view of the current sequence that only contains elements that\n  match the given path. An element matches the selector path if calling select\n  on that element with the path yields anything other than an empty sequence.\n\n  For transformation: `NONE` entries in the result sequence cause corresponding entries in\n  input to be removed. A result sequence smaller than the input sequence is equivalent to\n  padding the result sequence with `NONE` at the end until the same size as the input."
 [& path]
 (subselect ALL (selected? path)))

(defdynamicnav
 transformed
 "Navigates to a view of the current value by transforming it with the\n   specified path and update-fn."
 [path update-fn]
 (late-bound-nav
  [late (late-path path) late-fn update-fn]
  (select*
   [this structure next-fn]
   (next-fn (compiled-transform late late-fn structure)))
  (transform*
   [this structure next-fn]
   (next-fn (compiled-transform late late-fn structure)))))

(defdynamicnav
 traversed
 "Navigates to a view of the current value by transforming with a reduction over\n   the specified traversal."
 [path reduce-fn]
 (late-bound-nav
  [late (late-path path) late-fn reduce-fn]
  (select*
   [this structure next-fn]
   (next-fn (reduce late-fn (compiled-traverse late structure))))
  (transform*
   [this structure next-fn]
   (next-fn (reduce late-fn (compiled-traverse late structure))))))

(def
 ^{:doc "Keeps the element only if it matches the supplied predicate. Functions in paths\n          implicitly convert to this navigator.", :direct-nav true} pred
 com.rpl.specter.impl/pred*)

(defn
 ^{:direct-nav true} pred=
 [v]
 (pred (fn* [p1__1113#] (= p1__1113# v))))

(defn
 ^{:direct-nav true} pred<
 [v]
 (pred (fn* [p1__1114#] (< p1__1114# v))))

(defn
 ^{:direct-nav true} pred>
 [v]
 (pred (fn* [p1__1115#] (> p1__1115# v))))

(defn
 ^{:direct-nav true} pred<=
 [v]
 (pred (fn* [p1__1116#] (<= p1__1116# v))))

(defn
 ^{:direct-nav true} pred>=
 [v]
 (pred (fn* [p1__1117#] (>= p1__1117# v))))

(extend-type nil ImplicitNav (implicit-nav [this] STAY))

(extend-type
 clojure.lang.Keyword
 ImplicitNav
 (implicit-nav [this] (com.rpl.specter.navs/keypath* this)))

(extend-type
 clojure.lang.Symbol
 ImplicitNav
 (implicit-nav [this] (com.rpl.specter.navs/keypath* this)))

(extend-type
 String
 ImplicitNav
 (implicit-nav [this] (com.rpl.specter.navs/keypath* this)))

(extend-type
 System.ValueType
 ImplicitNav
 (implicit-nav [this] (com.rpl.specter.navs/keypath* this)))

nil

nil

(extend-type
 clojure.lang.AFn
 ImplicitNav
 (implicit-nav [this] (pred this)))

(extend-type
 clojure.lang.PersistentHashSet
 ImplicitNav
 (implicit-nav [this] (pred this)))

(extend-type
 System.Text.RegularExpressions.Regex
 ImplicitNav
 (implicit-nav [this] (regex-nav this)))

(defnav
 ^{:doc "Navigates to the provided val if the structure is nil. Otherwise it stays\n          navigated at the structure."} nil->val
 [v]
 (select*
  [this structure next-fn]
  (next-fn (if (nil? structure) v structure)))
 (transform*
  [this structure next-fn]
  (next-fn (if (nil? structure) v structure))))

(def
 ^{:doc "Navigates to #{} if the value is nil. Otherwise it stays\n          navigated at the current value."} NIL->SET
 (nil->val #{}))

(def
 ^{:doc "Navigates to '() if the value is nil. Otherwise it stays\n          navigated at the current value."} NIL->LIST
 (nil->val '()))

(def
 ^{:doc "Navigates to [] if the value is nil. Otherwise it stays\n          navigated at the current value."} NIL->VECTOR
 (nil->val []))

(defnav
 ^{:doc "Navigates to the metadata of the structure, or nil if\n  the structure has no metadata or may not contain metadata."} META
 []
 (select* [this structure next-fn] (next-fn (meta structure)))
 (transform*
  [this structure next-fn]
  (with-meta structure (next-fn (meta structure)))))

(defnav
 ^{:doc "Navigates to the name portion of the keyword or symbol"} NAME
 []
 (select* [this structure next-fn] (next-fn (name structure)))
 (transform*
  [this structure next-fn]
  (let
   [new-name (next-fn (name structure)) ns (namespace structure)]
   (cond
    (keyword? structure)
    (keyword ns new-name)
    (symbol? structure)
    (symbol ns new-name)
    :else
    (throw
     (ex-info
      "NAME can only be used on symbols or keywords"
      {:structure structure}))))))

(defnav
 ^{:doc "Navigates to the namespace portion of the keyword or symbol"} NAMESPACE
 []
 (select* [this structure next-fn] (next-fn (namespace structure)))
 (transform*
  [this structure next-fn]
  (let
   [name (name structure) new-ns (next-fn (namespace structure))]
   (cond
    (keyword? structure)
    (keyword new-ns name)
    (symbol? structure)
    (symbol new-ns name)
    :else
    (throw
     (ex-info
      "NAMESPACE can only be used on symbols or keywords"
      {:structure structure}))))))

(defdynamicnav
 ^{:doc "Adds the result of running select with the given path on the\n          current value to the collected vals."} collect
 [& path]
 (late-bound-collector
  [late (late-path path)]
  (collect-val [this structure] (compiled-select late structure))))

(defdynamicnav
 ^{:doc "Adds the result of running select-one with the given path on the\n          current value to the collected vals."} collect-one
 [& path]
 (late-bound-collector
  [late (late-path path)]
  (collect-val [this structure] (compiled-select-one late structure))))

(defcollector
 ^{:doc "Adds an external value to the collected vals. Useful when additional arguments\n     are required to the transform function that would otherwise require partial\n     application or a wrapper function.\n\n     e.g., incrementing val at path [:a :b] by 3:\n     (transform [:a :b (putval 3)] + some-map)"} putval
 [val]
 (collect-val [this structure] val))

(defdynamicnav
 ^{:doc "Continues navigating on the given path with the collected vals reset to []. Once\n     navigation leaves the scope of with-fresh-collected, the collected vals revert\n     to what they were before."} with-fresh-collected
 [& path]
 (late-bound-richnav
  [late (late-path path)]
  (select*
   [this vals structure next-fn]
   (com.rpl.specter.impl/exec-select*
    late
    []
    structure
    (fn [_ structure] (next-fn vals structure))))
  (transform*
   [this vals structure next-fn]
   (com.rpl.specter.impl/exec-transform*
    late
    []
    structure
    (fn [_ structure] (next-fn vals structure))))))

(defrichnav
 ^{:doc "Drops all collected values for subsequent navigation."} DISPENSE
 []
 (select* [this vals structure next-fn] (next-fn [] structure))
 (transform* [this vals structure next-fn] (next-fn [] structure)))

(defdynamicnav
 if-path
 "Like cond-path, but with if semantics."
 ([cond-p then-path] (if-path cond-p then-path STOP))
 ([cond-p then-path else-path]
  (if-let
   [afn (com.rpl.specter.navs/extract-basic-filter-fn cond-p)]
   (late-bound-richnav
    [late-then (late-path then-path) late-else (late-path else-path)]
    (select*
     [this vals structure next-fn]
     (com.rpl.specter.navs/if-select
      vals
      structure
      next-fn
      afn
      late-then
      late-else))
    (transform*
     [this vals structure next-fn]
     (com.rpl.specter.navs/if-transform
      vals
      structure
      next-fn
      afn
      late-then
      late-else)))
   (late-bound-richnav
    [late-cond
     (late-path cond-p)
     late-then
     (late-path then-path)
     late-else
     (late-path else-path)]
    (select*
     [this vals structure next-fn]
     (com.rpl.specter.navs/if-select
      vals
      structure
      next-fn
      (fn*
       [p1__1118#]
       (com.rpl.specter.navs/selected?* late-cond vals p1__1118#))
      late-then
      late-else))
    (transform*
     [this vals structure next-fn]
     (com.rpl.specter.navs/if-transform
      vals
      structure
      next-fn
      (fn*
       [p1__1119#]
       (com.rpl.specter.navs/selected?* late-cond vals p1__1119#))
      late-then
      late-else))))))

(defdynamicnav
 cond-path
 "Takes in alternating cond-path path cond-path path...\n   Tests the structure if selecting with cond-path returns anything.\n   If so, it uses the following path for this portion of the navigation.\n   Otherwise, it tries the next cond-path. If nothing matches, then the structure\n   is not selected."
 [& conds]
 (let
  [pairs (reverse (partition 2 conds))]
  (reduce (fn [p [tester apath]] (if-path tester apath p)) STOP pairs)))

(defdynamicnav
 multi-path
 "A path that branches on multiple paths. For updates,\n   applies updates to the paths in order."
 ([] STAY)
 ([path] path)
 ([path1 path2]
  (late-bound-richnav
   [late1 (late-path path1) late2 (late-path path2)]
   (select*
    [this vals structure next-fn]
    (let
     [res1
      (com.rpl.specter.impl/exec-select* late1 vals structure next-fn)]
     (if
      (reduced? res1)
      res1
      (let
       [res2
        (com.rpl.specter.impl/exec-select*
         late2
         vals
         structure
         next-fn)]
       (if (identical? NONE res1) res2 res1)))))
   (transform*
    [this vals structure next-fn]
    (let
     [s1
      (com.rpl.specter.impl/exec-transform*
       late1
       vals
       structure
       next-fn)]
     (com.rpl.specter.impl/exec-transform* late2 vals s1 next-fn)))))
 ([path1 path2 & paths]
  (reduce multi-path (multi-path path1 path2) paths)))

(defdynamicnav
 stay-then-continue
 "Navigates to the current element and then navigates via the provided path.\n   This can be used to implement pre-order traversal."
 [& path]
 (multi-path STAY path))

(defdynamicnav
 continue-then-stay
 "Navigates to the provided path and then to the current element. This can be used\n   to implement post-order traversal."
 [& path]
 (multi-path path STAY))

(def
 ^{:doc "Navigate the data structure until reaching\n          a value for which `afn` returns truthy. Has\n          same semantics as clojure.walk."} walker
 (recursive-path [afn] p (cond-path (pred afn) STAY coll? [ALL p])))

(def
 ^{:doc "Like `walker` but maintains metadata of any forms traversed."} codewalker
 (recursive-path
  [afn]
  p
  (cond-path (pred afn) STAY coll? [ALL-WITH-META p])))

(let
 [empty->NONE
  (if-path empty? (terminal-val NONE))
  compact*
  (fn [nav] (multi-path nav empty->NONE))]
 (defdynamicnav
  compact
  "During transforms, after each step of navigation in subpath check if the\n    value is empty. If so, remove that value by setting it to NONE."
  [& path]
  (map compact* path)))


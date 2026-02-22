(ns
 com.rpl.specter.navs
 (:use
  [com.rpl.specter.macros :only [defnav defrichnav]]
  [com.rpl.specter.util-macros :only [doseqres]])
 (:require [com.rpl.specter.impl :as i] [clojure.core.reducers :as r]))

(defn
 not-selected?*
 [compiled-path vals structure]
 (->>
  structure
  (com.rpl.specter.impl/compiled-select-any* compiled-path vals)
  (identical? com.rpl.specter.impl/NONE)))

(defn
 selected?*
 [compiled-path vals structure]
 (not (not-selected?* compiled-path vals structure)))

(defn
 all-select
 [structure next-fn]
 (doseqres com.rpl.specter.impl/NONE [e structure] (next-fn e)))

(defn queue? [coll] (instance? clojure.lang.PersistentQueue coll))

(defprotocol AllTransformProtocol (all-transform [structure next-fn]))

(defn
 void-transformed-kv-pair?
 [newkv]
 (or (identical? newkv com.rpl.specter.impl/NONE) (< (count newkv) 2)))

(defn-
 non-transient-map-all-transform
 [structure next-fn empty-map]
 (reduce-kv
  (fn
   [m k v]
   (let
    [newkv (next-fn [k v])]
    (if
     (void-transformed-kv-pair? newkv)
     m
     (assoc m (nth newkv 0) (nth newkv 1)))))
  empty-map
  structure))

(defn not-NONE? [v] (-> v (identical? com.rpl.specter.impl/NONE) not))

(defn-
 all-transform-list
 [structure next-fn]
 (doall (sequence (comp (map next-fn) (filter not-NONE?)) structure)))

(defn-
 all-transform-record
 [structure next-fn]
 (reduce (fn [res kv] (conj res (next-fn kv))) structure structure))

(extend-protocol
 AllTransformProtocol
 nil
 (all-transform [structure next-fn] nil)
 clojure.lang.MapEntry
 (all-transform
  [structure next-fn]
  (let
   [newk (next-fn (key structure)) newv (next-fn (val structure))]
   (clojure.lang.MapEntry. newk newv)))
 clojure.lang.IPersistentVector
 (all-transform
  [structure next-fn]
  (into [] (comp (map next-fn) (filter not-NONE?)) structure))
 String
 (all-transform
  [structure next-fn]
  (apply
   str
   (into [] (comp (map next-fn) (filter not-NONE?)) structure)))
 clojure.lang.PersistentHashSet
 (all-transform
  [structure next-fn]
  (into #{} (comp (map next-fn) (filter not-NONE?)) structure))
 clojure.lang.PersistentArrayMap
 clojure.lang.PersistentTreeMap
 (all-transform
  [structure next-fn]
  (non-transient-map-all-transform
   structure
   next-fn
   (empty structure)))
 clojure.lang.IRecord
 (all-transform
  [structure next-fn]
  (all-transform-record structure next-fn))
 clojure.lang.PersistentHashMap
 (all-transform
  [structure next-fn]
  (persistent!
   (reduce-kv
    (fn
     [m k v]
     (let
      [newkv (next-fn [k v])]
      (if
       (void-transformed-kv-pair? newkv)
       m
       (assoc! m (nth newkv 0) (nth newkv 1)))))
    (transient clojure.lang.PersistentHashMap/EMPTY)
    structure)))
 Object
 (all-transform
  [structure next-fn]
  (let
   [empty-structure (empty structure)]
   (cond
    (and (list? empty-structure) (not (queue? empty-structure)))
    (all-transform-list structure next-fn)
    (map? structure)
    (reduce-kv
     (fn
      [m k v]
      (let
       [newkv (next-fn [k v])]
       (if
        (void-transformed-kv-pair? newkv)
        m
        (assoc m (nth newkv 0) (nth newkv 1)))))
     empty-structure
     structure)
    :else
    (->>
     structure
     (clojure.core.reducers/map next-fn)
     (clojure.core.reducers/filter not-NONE?)
     (into empty-structure))))))

(defprotocol
 MapTransformProtocol
 (map-vals-transform [structure next-fn])
 (map-keys-transform [structure next-fn]))

(defn
 map-vals-non-transient-transform
 [structure empty-map next-fn]
 (reduce-kv
  (fn
   [m k v]
   (let
    [newv (next-fn v)]
    (if
     (identical? newv com.rpl.specter.impl/NONE)
     m
     (assoc m k newv))))
  empty-map
  structure))

(defn
 map-keys-non-transient-transform
 [structure empty-map next-fn]
 (reduce-kv
  (fn
   [m k v]
   (let
    [newk (next-fn k)]
    (if
     (identical? newk com.rpl.specter.impl/NONE)
     m
     (assoc m newk v))))
  empty-map
  structure))

(extend-protocol
 MapTransformProtocol
 nil
 (map-vals-transform [structure next-fn] nil)
 (map-keys-transform [structure next-fn] nil)
 clojure.lang.PersistentArrayMap
 (map-vals-transform
  [structure next-fn]
  (let
   [k-it
    (.keyIterator structure)
    v-it
    (.valIterator structure)
    none-cell
    (com.rpl.specter.impl/mutable-cell 0)
    len
    (.count structure)
    array
    (com.rpl.specter.impl/fast-object-array (* 2 len))]
   (loop
    [i 0 j 0]
    (if
     (.hasNext k-it)
     (let
      [k (.next k-it) v (.next v-it) newv (next-fn v)]
      (if
       (identical? newv com.rpl.specter.impl/NONE)
       (do
        (com.rpl.specter.impl/update-cell! none-cell inc)
        (recur (+ i 2) j))
       (do
        (aset array j k)
        (aset array (inc j) newv)
        (recur (+ i 2) (+ j 2)))))))
   (let
    [none-count
     (com.rpl.specter.impl/get-cell none-cell)
     array
     (if
      (not= 0 none-count)
      (com.rpl.specter.impl/array-copy-of
       array
       (* 2 (- len none-count)))
      array)]
    (clojure.lang.PersistentArrayMap. array))))
 (map-keys-transform
  [structure next-fn]
  (let
   [k-it
    (.keyIterator structure)
    v-it
    (.valIterator structure)
    none-cell
    (com.rpl.specter.impl/mutable-cell 0)
    len
    (.count structure)
    array
    (com.rpl.specter.impl/fast-object-array (* 2 len))]
   (loop
    [i 0 j 0]
    (if
     (.hasNext k-it)
     (let
      [k (.next k-it) v (.next v-it) newk (next-fn k)]
      (if
       (identical? newk com.rpl.specter.impl/NONE)
       (do
        (com.rpl.specter.impl/update-cell! none-cell inc)
        (recur (+ i 2) j))
       (do
        (aset array j newk)
        (aset array (inc j) v)
        (recur (+ i 2) (+ j 2)))))))
   (let
    [none-count
     (com.rpl.specter.impl/get-cell none-cell)
     array
     (if
      (not= 0 none-count)
      (com.rpl.specter.impl/array-copy-of
       array
       (* 2 (- len none-count)))
      array)]
    (clojure.lang.PersistentArrayMap/createAsIfByAssoc array))))
 clojure.lang.PersistentTreeMap
 (map-vals-transform
  [structure next-fn]
  (map-vals-non-transient-transform
   structure
   (empty structure)
   next-fn))
 (map-keys-transform
  [structure next-fn]
  (map-keys-non-transient-transform
   structure
   (empty structure)
   next-fn))
 clojure.lang.PersistentHashMap
 (map-vals-transform
  [structure next-fn]
  (persistent!
   (reduce-kv
    (fn
     [m k v]
     (let
      [newv (next-fn v)]
      (if
       (identical? newv com.rpl.specter.impl/NONE)
       m
       (assoc! m k newv))))
    (transient clojure.lang.PersistentHashMap/EMPTY)
    structure)))
 (map-keys-transform
  [structure next-fn]
  (persistent!
   (reduce-kv
    (fn
     [m k v]
     (let
      [newk (next-fn k)]
      (if
       (identical? newk com.rpl.specter.impl/NONE)
       m
       (assoc! m newk v))))
    (transient clojure.lang.PersistentHashMap/EMPTY)
    structure)))
 Object
 (map-vals-transform
  [structure next-fn]
  (reduce-kv
   (fn
    [m k v]
    (let
     [newv (next-fn v)]
     (if
      (identical? newv com.rpl.specter.impl/NONE)
      m
      (assoc m k newv))))
   (empty structure)
   structure))
 (map-keys-transform
  [structure next-fn]
  (reduce-kv
   (fn
    [m k v]
    (let
     [newk (next-fn k)]
     (if
      (identical? newk com.rpl.specter.impl/NONE)
      m
      (assoc m newk v))))
   (empty structure)
   structure)))

(defn
 srange-select
 [structure start end next-fn]
 (next-fn
  (if
   (string? structure)
   (subs structure start end)
   (-> structure vec (subvec start end)))))

(def srange-transform com.rpl.specter.impl/srange-transform*)

(defn
 extract-basic-filter-fn
 [path]
 (cond
  (fn? path)
  path
  (and (coll? path) (every? fn? path))
  (reduce
   (fn
    [combined afn]
    (fn [structure] (and (combined structure) (afn structure))))
   path)))

(defn
 if-select
 [vals structure next-fn then-tester then-nav else-nav]
 (com.rpl.specter.impl/exec-select*
  (if (then-tester structure) then-nav else-nav)
  vals
  structure
  next-fn))

(defn
 if-transform
 [vals structure next-fn then-tester then-nav else-nav]
 (com.rpl.specter.impl/exec-transform*
  (if (then-tester structure) then-nav else-nav)
  vals
  structure
  next-fn))

(defprotocol
 AddExtremes
 (append-all [structure elements])
 (prepend-all [structure elements])
 (append-one [structure elem])
 (prepend-one [structure elem]))

(extend-protocol
 AddExtremes
 nil
 (append-all [_ elements] elements)
 (prepend-all [_ elements] elements)
 (append-one [_ elem] (list elem))
 (prepend-one [_ elem] (list elem))
 clojure.lang.IPersistentVector
 (append-all [structure elements] (reduce conj structure elements))
 (prepend-all
  [structure elements]
  (let
   [ret (transient [])]
   (as->
    ret
    <>
    (reduce conj! <> elements)
    (reduce conj! <> structure)
    (persistent! <>))))
 (append-one [structure elem] (conj structure elem))
 (prepend-one [structure elem] (into [elem] structure))
 Object
 (append-all [structure elements] (concat structure elements))
 (prepend-all [structure elements] (concat elements structure))
 (append-one [structure elem] (concat structure [elem]))
 (prepend-one [structure elem] (cons elem structure)))

(defprotocol
 UpdateExtremes
 (update-first [s afn])
 (update-last [s afn]))

(defprotocol GetExtremes (get-first [s]) (get-last [s]))

(defprotocol FastEmpty (fast-empty? [s]))

(defprotocol InsertBeforeIndex (insert-before-idx [aseq idx val]))

(defnav
 PosNavigator
 [getter updater]
 (select*
  [this structure next-fn]
  (if-not
   (fast-empty? structure)
   (next-fn (getter structure))
   com.rpl.specter.impl/NONE))
 (transform*
  [this structure next-fn]
  (if (fast-empty? structure) structure (updater structure next-fn))))

(defn vec-count [v] (count v))

(defn-
 update-first-list
 [l afn]
 (let
  [newf (afn (first l)) restl (rest l)]
  (if
   (identical? com.rpl.specter.impl/NONE newf)
   restl
   (cons newf restl))))

(defn-
 update-last-list
 [l afn]
 (let
  [lastl (afn (last l)) bl (butlast l)]
  (if
   (identical? com.rpl.specter.impl/NONE lastl)
   (if (nil? bl) '() bl)
   (concat bl [lastl]))))

(defn-
 update-first-vector
 [v afn]
 (let
  [val (nth v 0) newv (afn val)]
  (if
   (identical? com.rpl.specter.impl/NONE newv)
   (subvec v 1)
   (assoc v 0 newv))))

(defn-
 update-last-vector
 [v afn]
 (let
  [c (int (vec-count v))]
  (case
   c
   1
   (let
    [[e] v newe (afn e)]
    (if (identical? com.rpl.specter.impl/NONE newe) [] [newe]))
   2
   (let
    [[e1 e2] v newe (afn e2)]
    (if (identical? com.rpl.specter.impl/NONE newe) [e1] [e1 newe]))
   (let
    [i (dec c) newe (afn (nth v i))]
    (if
     (identical? com.rpl.specter.impl/NONE newe)
     (pop v)
     (assoc v i newe))))))

(defn transient-vec-count [v] (count v))

(extend-protocol
 UpdateExtremes
 clojure.lang.IPersistentVector
 (update-first [v afn] (update-first-vector v afn))
 (update-last [v afn] (update-last-vector v afn))
 String
 (update-first
  [s afn]
  (let
   [rests (subs s 1 (count s)) newb (afn (nth s 0))]
   (if
    (identical? com.rpl.specter.impl/NONE newb)
    rests
    (str newb rests))))
 (update-last
  [s afn]
  (let
   [last-idx
    (-> s count dec)
    newl
    (afn (nth s last-idx))
    begins
    (subs s 0 last-idx)]
   (if
    (identical? com.rpl.specter.impl/NONE newl)
    begins
    (str begins newl))))
 Object
 (update-first [l val] (update-first-list l val))
 (update-last [l val] (update-last-list l val)))

(extend-protocol
 GetExtremes
 clojure.lang.IPersistentVector
 (get-first [v] (nth v 0))
 (get-last [v] (peek v))
 Object
 (get-first [s] (first s))
 (get-last [s] (last s))
 String
 (get-first [s] (nth s 0))
 (get-last [s] (nth s (-> s count dec))))

(extend-protocol
 FastEmpty
 nil
 (fast-empty? [_] true)
 clojure.lang.IPersistentVector
 (fast-empty? [v] (= 0 (vec-count v)))
 clojure.lang.ITransientVector
 (fast-empty? [v] (= 0 (transient-vec-count v)))
 Object
 (fast-empty? [s] (empty? s)))

(defn-
 do-keypath-transform
 [vals structure key next-fn]
 (let
  [newv (next-fn vals (get structure key))]
  (if
   (identical? newv com.rpl.specter.impl/NONE)
   (if
    (sequential? structure)
    (com.rpl.specter.impl/srange-transform*
     structure
     key
     (inc key)
     (fn [_] []))
    (dissoc structure key))
   (assoc structure key newv))))

(defrichnav
 ^{:doc "Navigates to the specified key, navigating to nil if it does not exist.\n          Setting the value to NONE will remove it from the collection."} keypath*
 [key]
 (select*
  [this vals structure next-fn]
  (next-fn vals (get structure key)))
 (transform*
  [this vals structure next-fn]
  (do-keypath-transform vals structure key next-fn)))

(defrichnav
 ^{:doc "Navigates to the key only if it exists in the map. Setting the value to NONE\n          will remove it from the collection."} must*
 [k]
 (select*
  [this vals structure next-fn]
  (if
   (contains? structure k)
   (next-fn vals (get structure k))
   com.rpl.specter.impl/NONE))
 (transform*
  [this vals structure next-fn]
  (if
   (contains? structure k)
   (do-keypath-transform vals structure k next-fn)
   structure)))

(defrichnav
 nthpath*
 ^{:doc
   "Navigates to the given position in the sequence. Setting the value to NONE\n          will remove it from the sequence. Works for all sequence types."}
 [i]
 (select*
  [this vals structure next-fn]
  (next-fn vals (nth structure i)))
 (transform*
  [this vals structure next-fn]
  (if
   (vector? structure)
   (let
    [newv (next-fn vals (nth structure i))]
    (if
     (identical? newv com.rpl.specter.impl/NONE)
     (com.rpl.specter.impl/srange-transform*
      structure
      i
      (inc i)
      (fn [_] []))
     (assoc structure i newv)))
   (com.rpl.specter.impl/srange-transform*
    structure
    i
    (inc i)
    (fn
     [[e]]
     (let
      [v (next-fn vals e)]
      (if (identical? v com.rpl.specter.impl/NONE) [] [v])))))))

(defrecord SrangeEndFunction [end-fn])

(defn
 invoke-end-fn
 [end-fn structure start]
 (if
  (instance? SrangeEndFunction end-fn)
  ((:end-fn end-fn) structure start)
  (end-fn structure)))

(defn-
 insert-before-index-list
 [lst idx val]
 (let [[front back] (split-at idx lst)] (concat front (cons val back))))

(extend-protocol
 InsertBeforeIndex
 nil
 (insert-before-idx
  [_ idx val]
  (if
   (= 0 idx)
   (list val)
   (throw
    (ex-info
     "For a nil structure, can only insert before index 0"
     {:insertion-index idx}))))
 String
 (insert-before-idx
  [aseq idx val]
  (apply str (insert-before-index-list aseq idx val)))
 clojure.lang.LazySeq
 (insert-before-idx
  [aseq idx val]
  (insert-before-index-list aseq idx val))
 clojure.lang.IPersistentVector
 (insert-before-idx
  [aseq idx val]
  (let
   [front (subvec aseq 0 idx) back (subvec aseq idx)]
   (into (conj front val) back)))
 clojure.lang.IPersistentList
 (insert-before-idx
  [aseq idx val]
  (cond
   (= idx 0)
   (cons val aseq)
   :else
   (insert-before-index-list aseq idx val))))


(ns
 com.rpl.specter.transients
 (:use [com.rpl.specter :only [defnav]])
 (:require
  [com.rpl.specter.navs :as n]
  [com.rpl.specter :refer [subselect selected?]]))

(defnav
 ^{:doc "Navigates to the specified key of a transient collection,\n          navigating to nil if it doesn't exist."} keypath!
 [key]
 (select* [this structure next-fn] (next-fn (get structure key)))
 (transform*
  [this structure next-fn]
  (assoc! structure key (next-fn (get structure key)))))

(defnav
 ^{:doc "Navigates to an empty (persistent) vector at the end of a transient vector."} END!
 []
 (select* [this structure next-fn] (next-fn []))
 (transform*
  [this structure next-fn]
  (let [res (next-fn [])] (reduce conj! structure res))))

(defn- t-get-first [tv] (nth tv 0))

(defn-
 t-get-last
 [tv]
 (nth tv (dec (com.rpl.specter.navs/transient-vec-count tv))))

(defn- t-update-first [tv next-fn] (assoc! tv 0 (next-fn (nth tv 0))))

(defn-
 t-update-last
 [tv next-fn]
 (let
  [i (dec (com.rpl.specter.navs/transient-vec-count tv))]
  (assoc! tv i (next-fn (nth tv i)))))

(def
 FIRST!
 "Navigates to the first element of a transient vector."
 (com.rpl.specter.navs/PosNavigator t-get-first t-update-first))

(def
 LAST!
 "Navigates to the last element of a transient vector."
 (com.rpl.specter.navs/PosNavigator t-get-last t-update-last))

(defn-
 select-keys-from-transient-map
 "Selects keys from transient map, because built-in select-keys uses\n  `find` which is unsupported."
 [m m-keys]
 (loop
  [result {} m-keys m-keys]
  (if-not
   (seq m-keys)
   result
   (let
    [k
     (first m-keys)
     item
     (get m k :com.rpl.specter.transients/not-found)]
    (recur
     (if-not
      (identical? item :com.rpl.specter.transients/not-found)
      (assoc result k item)
      result)
     (rest m-keys))))))

(defnav
 ^{:doc "Navigates to the specified persistent submap of a transient map."} submap!
 [m-keys]
 (select*
  [this structure next-fn]
  (next-fn (select-keys-from-transient-map structure m-keys)))
 (transform*
  [this structure next-fn]
  (let
   [selected
    (select-keys-from-transient-map structure m-keys)
    res
    (next-fn selected)]
   (as->
    structure
    %
    (reduce (fn [m k] (dissoc! m k)) % m-keys)
    (reduce-kv (fn [m k v] (assoc! m k v)) % res)))))


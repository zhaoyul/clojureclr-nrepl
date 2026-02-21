(ns
 com.rpl.specter.zipper
 (:use
  [com.rpl.specter
   :only
   [defnav nav declarepath providepath recursive-path]])
 (:require [com.rpl.specter :as s] [clojure.zip :as zip]))

(defnav
 zipper
 [constructor]
 (select* [this structure next-fn] (next-fn (constructor structure)))
 (transform*
  [this structure next-fn]
  (clojure.zip/root (next-fn (constructor structure)))))

(def VECTOR-ZIP (zipper clojure.zip/vector-zip))

(def SEQ-ZIP (zipper clojure.zip/seq-zip))

(def XML-ZIP (zipper clojure.zip/xml-zip))

(def
 ^{:doc "Navigate to the next element in the structure.\n             If no next element, works like STOP."} NEXT
 (com.rpl.specter/comp-paths
  (com.rpl.specter/view clojure.zip/next)
  (com.rpl.specter/if-path
   clojure.zip/end?
   com.rpl.specter/STOP
   com.rpl.specter/STAY)))

(defn-
 mk-zip-nav
 [znav]
 (nav
  []
  (select*
   [this structure next-fn]
   (let [ret (znav structure)] (if ret (next-fn ret))))
  (transform*
   [this structure next-fn]
   (let [ret (znav structure)] (if ret (next-fn ret) structure)))))

(def
 ^{:doc "Navigate to the element to the right.\n             If no element there, works like STOP."} RIGHT
 (mk-zip-nav clojure.zip/right))

(def
 ^{:doc "Navigate to the element to the left.\n             If no element there, works like STOP."} LEFT
 (mk-zip-nav clojure.zip/left))

(def DOWN (mk-zip-nav clojure.zip/down))

(def UP (mk-zip-nav clojure.zip/up))

(def
 ^{:doc "Navigate to the previous element.\n             If this is the first element, works like STOP."} PREV
 (mk-zip-nav clojure.zip/prev))

(def RIGHTMOST (com.rpl.specter/view clojure.zip/rightmost))

(def LEFTMOST (com.rpl.specter/view clojure.zip/leftmost))

(defn-
 inner-insert
 [structure next-fn inserter mover backer]
 (let
  [to-insert
   (next-fn [])
   inserts
   (reduce (fn [z e] (-> z (inserter e) mover)) structure to-insert)]
  (if backer (reduce (fn [z _] (backer z)) inserts to-insert) inserts)))

(defnav
 ^{:doc "Navigate to the empty subsequence directly to the\n                 right of this element."} INNER-RIGHT
 []
 (select* [this structure next-fn] (next-fn []))
 (transform*
  [this structure next-fn]
  (inner-insert
   structure
   next-fn
   clojure.zip/insert-right
   clojure.zip/right
   clojure.zip/left)))

(defnav
 ^{:doc "Navigate to the empty subsequence directly to the\n                 left of this element."} INNER-LEFT
 []
 (select* [this structure next-fn] (next-fn []))
 (transform*
  [this structure next-fn]
  (inner-insert
   structure
   next-fn
   clojure.zip/insert-left
   identity
   nil)))

(defnav
 NODE
 []
 (select*
  [this structure next-fn]
  (next-fn (clojure.zip/node structure)))
 (transform*
  [this structure next-fn]
  (clojure.zip/edit structure next-fn)))

(defnav
 ^{:doc "Navigate to the subsequence containing only\n                 the node currently pointed to. This works just\n                 like srange and can be used to remove elements\n                 from the structure"} NODE-SEQ
 []
 (select*
  [this structure next-fn]
  (next-fn [(clojure.zip/node structure)]))
 (transform*
  [this structure next-fn]
  (let
   [to-insert
    (next-fn [(clojure.zip/node structure)])
    inserted
    (reduce clojure.zip/insert-left structure to-insert)]
   (clojure.zip/remove inserted))))

(def
 ^{:doc "Navigate the zipper to the first element\n                     in the structure matching predfn. A linear scan\n                     is done using NEXT to find the element."} find-first
 (recursive-path
  [predfn]
  p
  (com.rpl.specter/if-path
   [NODE (com.rpl.specter/pred predfn)]
   com.rpl.specter/STAY
   [NEXT p])))

(declarepath
 ^{:doc "Navigate to every element reachable using calls\n                     to NEXT"} NEXT-WALK)

(providepath
 NEXT-WALK
 (com.rpl.specter/stay-then-continue NEXT NEXT-WALK))


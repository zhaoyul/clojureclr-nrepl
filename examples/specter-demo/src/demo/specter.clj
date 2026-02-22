(ns demo.specter
  (:require [com.rpl.specter :as s]))

(defn run []
  (let [data {:a 1 :b {:c 2 :d [1 2 3 4]}}
        sel (s/select [:b :d s/ALL] data)
        even-inc (s/transform [:b :d s/ALL even?] inc data)]
    {:select sel
     :transform even-inc}))

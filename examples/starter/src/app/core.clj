(ns app.core
  (:require [clojure.core.async :as async]
            [com.rpl.specter :as sp]))

(defn hello [name]
  (str "hello " name))

(defn specter_demo []
  (sp/select [sp/ALL] [1 2 3 4]))

(defn async_demo []
  (let [ch (async/chan 1)]
    (async/>!! ch 21)
    (str "blocking put/take => " (* 2 (async/<!! ch)))))

(defn async_map_demo []
  (let [in (async/chan 3)
        doubled (async/map #(* % 2) [in])]
    (async/>!! in 1)
    (async/>!! in 2)
    (async/>!! in 3)
    (async/close! in)
    (str "mapped channel => "
         "[" (async/<!! doubled) " " (async/<!! doubled) " " (async/<!! doubled) "]")))

(defn -main [& args]
    (let [name (or (first args) "starter")]
      (println (str "hello => " (hello name)))
      (println (str "specter_demo => " (specter_demo)))
      (println (str "async_demo => " (async_demo)))
      (println (str "async_map_demo => " (async_map_demo)))
      (when (seq args)
        (println (str "args => " (pr-str args))))))

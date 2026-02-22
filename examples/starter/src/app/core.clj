(ns app.core
  (:require [com.rpl.specter :as sp]))

(defn hello [name]
  (str "hello " name))

(defn specter_demo []
  (sp/select [sp/ALL] [1 2 3 4]))

(defn -main [& args]
  (let [name (or (first args) "starter")]
    (println (str "hello => " (hello name)))
    (println (str "specter_demo => " (specter_demo)))
    (when (seq args)
      (println (str "args => " (pr-str args))))))

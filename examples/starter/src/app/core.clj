(ns app.core
  (:require [com.rpl.specter :as sp]))

(defn hello [name]
  (str "hello " name))

(defn specter_demo []
  (sp/select [sp/ALL] [1 2 3 4]))

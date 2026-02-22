(ns demo.core
  (:require [clojure.core.async :as async]))

(defn run []
  (let [ch (async/chan 1)]
    (async/put! ch 42)
    (async/<!! ch)))

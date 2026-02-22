(ns demo.minimal)

(defn hello []
  "hello from ClojureCLR (minimal api)")

(defn health []
  "{\"ok\":true}")

(defn echo [s]
  (if (nil? s) "" s))

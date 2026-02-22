# Specter CLR Port

This is a ClojureCLR port of `com.rpl/specter`.

## Build

```bash
clojure -Sdeps '{:deps {org.clojure/tools.reader {:mvn/version "1.3.7"}}}' -e '(load-file "tools/gen-cljr.clj")'
dotnet build packages/specter-clr/specter-clr.csproj
```

Notes:
- The build embeds `.clj` resources (not `.cljc`) for ClojureCLR compatibility.
- `tools/gen-cljr.clj` generates CLR-friendly `.clj` files from `.cljc`. Re-run it after editing any `.cljc` files under `packages/specter-clr/src/clj`.
- The generated `.clj` files are derived artifacts; edit the `.cljc` sources instead.

## Pack (local)

```bash
clojure -Sdeps '{:deps {org.clojure/tools.reader {:mvn/version "1.3.7"}}}' -e '(load-file "tools/gen-cljr.clj")'
dotnet pack packages/specter-clr/specter-clr.csproj -c Release -o nupkgs
```

## Use in ClojureCLR

```clojure
(ns demo
  (:require [com.rpl.specter :as s]))

(s/select [:a :b s/ALL] {:a {:b [1 2 3]}})
```

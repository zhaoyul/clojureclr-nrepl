(ns com.rpl.specter.protocols)

(defprotocol
 RichNavigator
 "Do not use this protocol directly. All navigators must be created using macros\n  in com.rpl.specter namespace."
 (select*
  [this vals structure next-fn]
  "An implementation of `select*` must call `next-fn` on each\n     subvalue of `structure`. The result of `select*` is specified\n     as follows:\n\n     1. `NONE` if `next-fn` never called\n     2. `NONE` if all calls to `next-fn` return `NONE`\n     3. Otherwise, any non-`NONE` return value from calling `next-fn`\n     ")
 (transform*
  [this vals structure next-fn]
  "An implementation of `transform*` must use `next-fn` to transform\n     any subvalues of `structure` and then merge those transformed values\n     back into `structure`. Everything else in `structure` must be unchanged."))

(defprotocol
 Collector
 "Do not use this protocol directly. All navigators must be created using\n  macros in com.rpl.specter namespace."
 (collect-val [this structure]))

(defprotocol ImplicitNav (implicit-nav [obj]))


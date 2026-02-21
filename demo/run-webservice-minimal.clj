(ns demo.run-webservice-minimal
  (:import [System Environment Console]
           [System.IO Directory DirectoryInfo FileInfo Path StreamReader]
           [System.Reflection Assembly]
           [System.Text Encoding]))

(defn- find-repo-root [start-dir]
  (when (and start-dir (not= start-dir ""))
    (loop [dir (Path/GetFullPath start-dir)]
      (let [marker (Path/Combine dir "clojureCLR-nrepl.csproj")
            parent (Path/GetDirectoryName dir)]
        (cond
          (.Exists (FileInfo. marker)) dir
          (or (nil? parent) (= parent dir)) nil
          :else (recur parent))))))

(defn- repo-root []
  (let [from-file (when *file* (Path/GetDirectoryName *file*))
        from-cwd (Directory/GetCurrentDirectory)]
    (or (when from-file (find-repo-root from-file))
        (find-repo-root from-cwd)
        (throw (ex-info "Cannot locate repo root (clojureCLR-nrepl.csproj)" {})))))

(defn- combine-path [& parts]
  (reduce (fn [a b] (Path/Combine a b)) (first parts) (rest parts)))

(defn- dotnet-root-candidates []
  (let [env (Environment/GetEnvironmentVariable "DOTNET_ROOT")]
    (filter some?
            [env
             "/usr/local/share/dotnet"
             "/usr/share/dotnet"
             "C:\\Program Files\\dotnet"])))

(defn- latest-version-dir [base]
  (let [dir (DirectoryInfo. base)]
    (when (.Exists dir)
      (let [subs (.GetDirectories dir)]
        (when (seq subs)
          (->> subs
               (map #(.FullName %))
               (sort)
               last))))))

(defn- find-aspnet-dir []
  (let [candidates (for [root (dotnet-root-candidates)]
                     (Path/Combine root "shared" "Microsoft.AspNetCore.App"))
        existing (first (filter #(Directory/Exists %) candidates))]
    (when-not existing
      (throw (ex-info "Microsoft.AspNetCore.App not found under DOTNET_ROOT" {:candidates candidates})))
    (or (latest-version-dir existing)
        (throw (ex-info "No versions found for Microsoft.AspNetCore.App" {:base existing})))))

(defn- load-aspnet-assemblies! []
  (let [aspnet-dir (find-aspnet-dir)
        names ["Microsoft.AspNetCore.dll"
               "Microsoft.AspNetCore.Http.dll"
               "Microsoft.AspNetCore.Routing.dll"
               "Microsoft.Extensions.Hosting.dll"
               "Microsoft.Extensions.DependencyInjection.dll"]]
    (doseq [name names]
      (let [path (Path/Combine aspnet-dir name)]
        (when (.Exists (FileInfo. path))
          (Assembly/LoadFrom path))))))

(defn- find-type [^String full-name]
  (or (System.Type/GetType full-name)
      (some (fn [^Assembly asm] (.GetType asm full-name false))
            (.. System.AppDomain CurrentDomain GetAssemblies))
      (throw (ex-info (str "Type not found: " full-name) {}))))

(defn- method-by [^System.Type t name pred]
  (first (filter #(and (= name (.Name ^System.Reflection.MethodInfo %))
                       (pred %))
                 (.GetMethods t))))

(defn- to-delegate [f ^System.Type delegate-type]
  (clojure.lang.Runtime.Converter/ConvertToDelegate f delegate-type))

(defn- read-body [req]
  (with-open [sr (StreamReader. (.Body req) Encoding/UTF8)]
    (.. sr ReadToEndAsync GetAwaiter GetResult)))

(defn- respond-text [ctx status body content-type]
  (let [resp (.Response ctx)]
    (set! (.StatusCode resp) (int status))
    (set! (.ContentType resp) content-type)
    (let [ext-type (find-type "Microsoft.AspNetCore.Http.HttpResponseWritingExtensions")
          write-m (method-by ext-type "WriteAsync"
                             (fn [m]
                               (let [ps (.GetParameters m)]
                                 (and (= 3 (alength ps))
                                      (= "Microsoft.AspNetCore.Http.HttpResponse" (.FullName (.ParameterType (aget ps 0))))
                                      (= "System.String" (.FullName (.ParameterType (aget ps 1))))
                                      (= "System.Threading.CancellationToken" (.FullName (.ParameterType (aget ps 2))))))))]
      (.Invoke write-m nil (object-array [resp body (System.Threading.CancellationToken/None)])))))

(defn -main [& _]
  (load-aspnet-assemblies!)

  (let [root (repo-root)
        clj-file (combine-path root "demo" "webservice-minimal" "src" "demo" "minimal.clj")]
    (clojure.core/load-file clj-file))
  (require 'demo.minimal)

  (let [webapp-type (find-type "Microsoft.AspNetCore.Builder.WebApplication")
        builder-m (method-by webapp-type "CreateBuilder"
                             (fn [m]
                               (let [ps (.GetParameters m)]
                                 (and (= 1 (alength ps))
                                      (= "System.String[]" (.FullName (.ParameterType (aget ps 0))))))))
        args (make-array String 0)
        builder (.Invoke builder-m nil (object-array [args]))
        build-m (.GetMethod (.GetType builder) "Build")
        app (.Invoke build-m builder (object-array []))

        request-type (find-type "Microsoft.AspNetCore.Http.HttpRequest")
        request-delegate-type (find-type "Microsoft.AspNetCore.Http.RequestDelegate")
        map-ext-type (find-type "Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions")
        mapget (method-by map-ext-type "MapGet"
                          (fn [m]
                            (let [ps (.GetParameters m)]
                              (and (= 3 (alength ps))
                                   (= "System.String" (.FullName (.ParameterType (aget ps 1))))
                                   (= "Microsoft.AspNetCore.Http.RequestDelegate" (.FullName (.ParameterType (aget ps 2))))))))
        mappost (method-by map-ext-type "MapPost"
                           (fn [m]
                             (let [ps (.GetParameters m)]
                               (and (= 3 (alength ps))
                                    (= "System.String" (.FullName (.ParameterType (aget ps 1))))
                                    (= "Microsoft.AspNetCore.Http.RequestDelegate" (.FullName (.ParameterType (aget ps 2))))))))

        hello-var (ns-resolve 'demo.minimal 'hello)
        health-var (ns-resolve 'demo.minimal 'health)
        echo-var (ns-resolve 'demo.minimal 'echo)
        hello-fn (fn [ctx]
                   (respond-text ctx 200 (hello-var) "text/plain; charset=utf-8"))
        health-fn (fn [ctx]
                    (respond-text ctx 200 (health-var) "application/json; charset=utf-8"))
        echo-fn (fn [ctx]
                  (let [req (.Request ctx)
                        body (read-body req)]
                    (respond-text ctx 200 (echo-var body) "text/plain; charset=utf-8")))

        hello-del (to-delegate hello-fn request-delegate-type)
        health-del (to-delegate health-fn request-delegate-type)
        echo-del (to-delegate echo-fn request-delegate-type)]

    (.Invoke mapget nil (object-array [app "/" hello-del]))
    (.Invoke mapget nil (object-array [app "/health" health-del]))
    (.Invoke mappost nil (object-array [app "/echo" echo-del]))

    (let [app-type (.GetType app)
          ct-type (find-type "System.Threading.CancellationToken")
          start-async-m (.GetMethod app-type "StartAsync" (into-array System.Type [ct-type]))
          stop-async-m (.GetMethod app-type "StopAsync" (into-array System.Type [ct-type]))]
      (when start-async-m
        (let [task (.Invoke start-async-m app (object-array [(System.Threading.CancellationToken/None)]))]
          (.Wait ^System.Threading.Tasks.Task task)))
      (println "minimal api running (ASPNETCORE_URLS or default).")
      (println "Press Enter to stop...")
      (Console/ReadLine)
      (when stop-async-m
        (.Invoke stop-async-m app (object-array [(System.Threading.CancellationToken/None)]))))))

# C# Host Starter (Template)

这个 starter 是一个**可直接复制**的模板，用来在一个 .NET 项目里同时使用：

- C# NuGet 包（示例：`Newtonsoft.Json`）
- ClojureCLR 的 cljr 包（示例：`com.rpl.specter.clr`）
- Clojure core.async（示例：`clojure.core.async.clrfix`）
- 以及可选的 nREPL 服务器

## 结构

```
examples/starter/
├── starter.csproj
├── Program.cs
├── NuGet.config
└── src/
    └── app/
        └── core.clj
```

## 使用

在 `examples/starter` 目录运行：

```bash
dotnet run --project ./starter.csproj
```

如果要启动 nREPL：

```bash
NREPL_ENABLE=1 dotnet run --project ./starter.csproj
```

说明：

- `-main` 执行完毕后进程会退出。需要常驻时，请在 `-main` 中阻塞，或开启 nREPL 并保持运行（按你的实现方式决定）。
- 如果你的 Clojure 入口文件不在 `src/app/core.clj`，可设置环境变量 `CORECLR_STUDIO_CLJ_PATH` 指向任意 `.clj` 文件。

## 说明

- `NuGet.config` 默认使用官方 NuGet 源（`nuget.org`）。如果你要用本地调试版本，可在此文件中补充本地 feed。
- `Program.cs` 会预加载 `Clojure.dll`、`Clojure.Source.dll`、`clojure.core.async.dll` 等运行依赖，
  以确保 `require` 能找到嵌入的 clj 资源。
- `src/app/core.clj` 提供 `-main` 作为入口，C# Host 会直接加载并执行它（支持直接运行时从项目根目录查找，或从输出目录查找）。
- `src/app/core.clj` 演示了基本的 clj 函数、specter 与 core.async 的调用示例。

## core.async 示例

`src/app/core.clj` 已经包含两个 core.async 示例：

```clojure
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
```

运行后可看到：

```bash
hello => hello starter
specter_demo => [1 2 3 4]
async_demo => blocking put/take => 42
async_map_demo => mapped channel => [2 4 6]
json ok => True, n => 42
```

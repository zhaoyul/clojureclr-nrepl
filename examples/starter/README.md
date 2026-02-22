# C# Host Starter (Template)

这个 starter 是一个**可直接复制**的模板，用来在一个 .NET 项目里同时使用：

- C# NuGet 包（示例：`Newtonsoft.Json`）
- ClojureCLR 的 cljr 包（示例：`com.rpl.specter.clr`）
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

在仓库根目录运行：

```bash
dotnet run --project examples/starter/starter.csproj
```

如果要启动 nREPL：

```bash
NREPL_ENABLE=1 dotnet run --project examples/starter/starter.csproj
```

说明：如果 `-main` 执行完毕，进程会退出。需要常驻时，请在 `-main` 中阻塞，
或开启 nREPL 并保持运行（按你的实现方式决定）。

## 说明

- `NuGet.config` 默认把 `../../nupkgs` 作为本地源（本仓库内可直接用）。
  如果你把 starter 拷贝到其他目录，请调整该路径或移除该源。
- `Program.cs` 会预加载 `Clojure.dll`、`Clojure.Source.dll` 和 `com.rpl.specter.dll`，
  以确保 `require` 能找到嵌入的 clj 资源。
- `src/app/core.clj` 提供 `-main` 作为入口，C# Host 会直接调用它。
- `src/app/core.clj` 演示了基本的 clj 函数和 specter 调用。

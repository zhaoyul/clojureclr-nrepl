# ClojureCLR nREPL Server

一个基于 C# 实现的 nREPL 服务器，专为 ClojureCLR 设计。支持 CIDER、Calva 等标准 nREPL 客户端。

## 特性

- **完整协议支持**: Bencode 编码/解码，会话管理
- **CIDER 兼容**: 支持自动补全、文档查询、参数提示等 middleware
- **轻量级**: 单文件实现，零 Clojure 依赖（运行时自动加载）
- **跨平台**: 基于 .NET 8.0，支持 Windows、macOS、Linux
- **CLR 静态成员补全**: 支持 `Type/Member`（例如 `Enumerable/Where`），前提是已 `import` 对应类型

## 快速开始

```bash
dotnet run --project cli/clojureCLR-nrepl-cli.csproj
```

服务器将在 `127.0.0.1:1667` 启动。

### Demo（Clojure-only, core.async + nREPL）

先安装 Clojure.Main（一次）：
```bash
dotnet tool install --global Clojure.Main --version 1.12.3-alpha4
```

准备依赖与本地构建（一次）：
```bash
dotnet build -c Debug clojureCLR-nrepl.csproj
dotnet restore demo/clojureclr-demo.csproj
```

运行：
```bash
clojure -i demo/run-core.clj -e "(demo.run-core/-main)"
```

可选环境变量：
- `NREPL_ENABLE`（默认开启；设为 `0`/`false` 可禁用）
- `NREPL_HOST`（默认 `127.0.0.1`）
- `NREPL_PORT`（默认 `1667`）

注意：`clojure.core.async.clrfix` 在 CLR 上依赖 `clojure.tools.analyzer.clr`。以上脚本会从本机 NuGet cache 加载两者。
依赖清单（与 `demo/run-core.clj` 保持一致）：
- `clojure.core.async.clrfix` `1.7.701-clrfix2`
- `clojure.tools.analyzer.clr` `1.3.2`
- `clojure.tools.analyzer` `1.1.1`
- `clojure.tools.reader` `1.3.7`
- `clojure.core.memoize` `1.1.266`
- `clojure.core.cache` `1.1.234`
- `clojure.data.priority-map` `1.2.0`

### Demo（Clojure-only Webservice + .NET HttpListener）

使用 Clojure 直接驱动 .NET `HttpListener`，无需 C# Web 框架：

```bash
clojure -i demo/run-webservice.clj -e "(demo.run-webservice/-main)"
```

默认监听 `http://127.0.0.1:8080/`，可用环境变量覆盖：
```bash
WEB_HOST=127.0.0.1 WEB_PORT=8081 clojure -i demo/run-webservice.clj -e "(demo.run-webservice/-main)"
```

可选开启 nREPL（默认关闭）：
```bash
NREPL_ENABLE=1 NREPL_PORT=1667 clojure -i demo/run-webservice.clj -e "(demo.run-webservice/-main)"
```

路由示例：
- `GET /` → `hello from ClojureCLR`
- `GET /health` → `{"ok":true}`
- `POST /echo` → 原样返回请求体

### Demo（Clojure-only Specter）

准备依赖（一次）：
```bash
dotnet restore demo/specter-demo/specter-demo.csproj
```

运行：
```bash
clojure -i demo/run-specter.clj -e "(demo.run-specter/-main)"
```

可选开启 nREPL（默认关闭）：
```bash
NREPL_ENABLE=1 NREPL_PORT=1667 clojure -i demo/run-specter.clj -e "(demo.run-specter/-main)"
```

### Demo（Clojure-only Minimal API）

使用 ASP.NET Core Minimal API，入口与路由均为 Clojure 代码：

```bash
clojure -i demo/run-webservice-minimal.clj -e "(demo.run-webservice-minimal/-main)"
```

默认使用 `ASPNETCORE_URLS`（若未设置，按 ASP.NET Core 默认值）。例如：
```bash
ASPNETCORE_URLS=http://127.0.0.1:8082 clojure -i demo/run-webservice-minimal.clj -e "(demo.run-webservice-minimal/-main)"
```

可选开启 nREPL（默认关闭）：
```bash
NREPL_ENABLE=1 NREPL_PORT=1667 clojure -i demo/run-webservice-minimal.clj -e "(demo.run-webservice-minimal/-main)"
```

路由示例：
- `GET /` → `hello from ClojureCLR (minimal api)`
- `GET /health` → `{"ok":true}`
- `POST /echo` → 原样返回请求体

### 最简 REPL 起点

只启动 nREPL，适合作为所有项目的起点：

```bash
clojure -i demo/run-repl-only.clj -e "(demo.run-repl-only/-main)"
```

详细使用指南：
- [QUICKSTART.md](./QUICKSTART.md) - 5 分钟快速上手
- [USAGE.md](./USAGE.md) - 完整使用文档

### 连接客户端

**CIDER (Emacs):**
```elisp
M-x cider-connect-clj
Host: 127.0.0.1
Port: 1667
```

**Calva (VS Code):**
1. `Cmd/Ctrl+Shift+P` → "Calva: Connect to a Running REPL Server"
2. 选择 "nREPL"
3. 输入 `127.0.0.1:1667`

**命令行测试:**
```bash
# 使用 Python 测试脚本
python3 test_nrepl.py
```

## 支持的 nREPL 操作

### 核心操作

| 操作          | 说明           | 状态 |
|---------------|----------------|------|
| `eval`        | 代码求值       | ✅   |
| `clone`       | 创建新会话     | ✅   |
| `close`       | 关闭会话       | ✅   |
| `ls-sessions` | 列出会话       | ✅   |
| `describe`    | 服务器能力描述 | ✅   |
| `interrupt`   | 中断执行       | ✅   |
| `load-file`   | 加载文件       | ✅   |
| `stdin`       | 标准输入       | ✅   |

### Middleware 功能

| 操作       | 说明         | CIDER 使用场景         | 状态 |
|------------|--------------|------------------------|------|
| `complete` | 自动补全     | `M-TAB` 补全代码       | ✅   |
| `info`     | 符号信息查询 | `C-c C-d C-d` 查看文档 | ✅   |
| `eldoc`    | 函数参数提示 | Minibuffer 显示参数    | ✅   |

补全能力（摘要）:
- Clojure 命名空间中的 var/macro
- CLR 静态成员补全：`Type/Member`（已导入类型或完整类型名）
- 实例成员补全：`. 形式`（接收者为命名空间中可解析的符号）
- `info`/`eldoc` 支持 CLR 静态成员

## 技术实现

### 项目结构

```
.
├── src/
│   ├── BencodeCodec.cs                 # Bencode 编解码
│   ├── NReplServer.Core.cs             # 连接/协议/消息分发
│   ├── NReplServer.Eval.cs             # eval 与命名空间切换
│   ├── NReplServer.SessionHandlers.cs  # clone/ls-sessions/interrupt/load-file
│   ├── NReplServer.Completion.cs       # 补全入口
│   ├── NReplServer.Completion.Dot.cs   # 点调用补全
│   ├── NReplServer.Completion.Cache.cs # 反射缓存
│   ├── NReplServer.Info.cs             # info 处理
│   ├── NReplServer.Eldoc.cs            # eldoc 入口
│   ├── NReplServer.Eldoc.Dot.cs        # 点调用 eldoc
│   ├── NReplServer.Eldoc.Clr.cs        # CLR 静态成员 eldoc
│   ├── NReplServer.Utilities.cs        # 通用工具
│   ├── NReplServer.Parsing.cs          # 补全/eldoc 解析
│   └── NReplSession.cs                 # 会话模型
├── cli/
│   ├── Program.cs                      # CLI 入口
│   └── clojureCLR-nrepl-cli.csproj     # CLI 项目
├── tests/
│   ├── BencodeCodecTests.cs            # 编解码测试
│   ├── ServerIntegrationTests.cs       # 基本协议测试
│   └── clojureCLR-nrepl.Tests.csproj   # 测试项目
├── demo/
│   ├── run-core.clj                    # core.async + nREPL demo
│   ├── run-specter.clj                 # Specter demo
│   ├── run-webservice.clj              # HttpListener demo
│   ├── run-webservice-minimal.clj      # Minimal API demo
│   ├── run-repl-only.clj               # 最简 nREPL 起点
│   └── nrepl_util.clj                  # nREPL 启停工具
├── clojureCLR-nrepl.csproj             # 库项目（NuGet 包）
├── test_nrepl.py                       # Python 测试脚本
└── README.md                           # 本文档
```

### 核心组件

1. **Bencode 编解码器**: 支持字典、列表、整数、字符串
2. **会话管理**: 基于 GUID 的会话跟踪
3. **Namespace 切换**: 支持 `in-ns` 和跨会话命名空间保持
4. **Middleware 框架**: 可扩展的操作处理器

### 实现细节

```csharp
// 处理 nREPL 消息
private string HandleMessage(NetworkStream stream, Dictionary<string, object> request,
                             string sessionId, bool useLengthPrefix, Session session)
{
    var op = request.GetValueOrDefault("op") as string;
    switch (op)
    {
        case "eval": HandleEval(...); break;
        case "complete": HandleComplete(...); break;
        case "eldoc": HandleEldoc(...); break;
        // ...
    }
}
```

## 开发路线图

### 当前版本 (v0.1.x)

- ✅ 基础 nREPL 协议
- ✅ CIDER 基础 middleware (complete, info, eldoc)
- ✅ 会话管理
- ✅ 命名空间切换

### 计划功能

#### 🔴 高优先级

| 功能          | 说明                 | CIDER 集成              |
|---------------|----------------------|-------------------------|
| `ns-path`     | 查找命名空间文件路径 | 跳转到定义              |
| `apropos`     | 符号搜索             | `M-x cider-apropos`     |
| `macroexpand` | 宏展开               | `M-x cider-macroexpand` |
| `classpath`   | 类路径查询           | 检查依赖                |

#### 🟡 中优先级

| 功能              | 说明              | CIDER 集成                 |
|-------------------|-------------------|----------------------------|
| `refresh`/`clear` | 代码热重载        | `M-x cider-ns-refresh`     |
| `test`            | 运行测试          | `M-x cider-test-run-tests` |
| `format`          | 代码格式化        | `M-x cider-format-buffer`  |
| `spec`            | Clojure Spec 支持 | Spec 检查                  |

#### 🟢 低优先级

| 功能                      | 说明             | CIDER 集成 |
|---------------------------|------------------|------------|
| `fn-deps`/`fn-refs`       | 函数依赖/引用    | 代码分析   |
| `xrefs`                   | 交叉引用         | 重构支持   |
| `clojuredocs-lookup`      | 查询 ClojureDocs | 社区文档   |
| `analyze-last-stacktrace` | 异常堆栈分析     | 错误美化   |

### 技术债务

- [ ] 支持长度前缀格式的 Bencode 消息
- [ ] 更完善的错误处理和日志
- [ ] 配置选项（端口、主机、日志级别）
- [ ] 性能优化（大型消息处理）

## 贡献指南

### 添加新 Middleware

1. 在 `HandleMessage` 中添加新的 case:

```csharp
case "new-op":
    HandleNewOp(stream, request, sessionId, useLengthPrefix, session);
    break;
```

2. 实现处理器:

```csharp
private void HandleNewOp(NetworkStream stream, Dictionary<string, object> request,
                         string sessionId, bool useLengthPrefix, Session session)
{
    var id = GetId(request);
    // ... 实现逻辑

    SendResponse(stream, new Dictionary<string, object>
    {
        ["id"] = id,
        ["session"] = sessionId,
        ["result"] = result,
        ["status"] = new List<string> { "done" }
    }, useLengthPrefix);
}
```

3. 更新 `CreateDescribeResponse` 声明支持的操作

### 测试

```bash
# 运行服务器
dotnet run --project cli/clojureCLR-nrepl-cli.csproj &

# 运行单元测试
dotnet test tests/clojureCLR-nrepl.Tests.csproj -c Release

# 运行 Python 测试
python3 test_nrepl.py

# 或使用 clj-nrepl-eval
clj-nrepl-eval --port 1667 "(+ 1 2 3)"
```

## 使用场景

### 1. ClojureCLR 开发
直接连接 CIDER/Calva 进行 ClojureCLR 项目开发，享受完整的 IDE 支持。

### 2. Unity 游戏开发
将服务器嵌入 Unity 项目，运行时热重载 Clojure 脚本。

### 3. .NET 应用脚本化
为现有 .NET 应用提供 Clojure 脚本能力，nREPL 作为管理接口。

### 4. 数据分析与探索
结合 Clojure 的数据处理能力和 .NET 的性能，进行交互式数据分析。

## 相关项目

- [ClojureCLR](https://github.com/clojure/clojure-clr) - Clojure 的 .NET 实现
- [nREPL](https://nrepl.org/) - nREPL 协议规范
- [CIDER](https://github.com/clojure-emacs/cider) - Emacs 的 Clojure IDE
- [Calva](https://github.com/BetterThanTomorrow/calva) - VS Code 的 Clojure 插件

## 许可证

MIT License - 详见 LICENSE 文件

# 使用指南

本文档介绍如何在不同场景中使用 ClojureCLR nREPL Server。

## 目录

1. [作为独立程序运行](#1-作为独立程序运行)
2. [作为 .NET 库引用](#2-作为-net-库引用)
3. [集成到现有应用](#3-集成到现有应用)
4. [配置选项](#4-配置选项)
5. [最佳实践](#5-最佳实践)

---

## 1. 作为独立程序运行

最简单的方式，适合开发和调试。

### 从源码运行

```bash
# 克隆项目
git clone <your-repo-url>
cd clojureCLR-nrepl

# 运行
dotnet run --project cli/clojureCLR-nrepl-cli.csproj

# 后台运行（Linux/macOS）
dotnet run --project cli/clojureCLR-nrepl-cli.csproj &

# Windows 后台运行
start dotnet run --project cli/clojureCLR-nrepl-cli.csproj
```

### 使用发布版本

```bash
# 发布单文件可执行文件
dotnet publish -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o ./publish

# 运行
./publish/clojureCLR-nrepl
```

### 连接到服务器

**Emacs + CIDER:**
```elisp
M-x cider-connect-clj
Host: 127.0.0.1
Port: 1667
```

**VS Code + Calva:**
1. `Cmd/Ctrl+Shift+P` → "Calva: Connect to a Running REPL Server"
2. 选择 "nREPL"
3. 输入 `127.0.0.1:1667`

**命令行测试:**
```bash
# 使用 Python 测试脚本
python3 test_nrepl.py

# 或使用 netcat 发送原始消息
echo 'd2:op5:clone2:id1:1e' | nc localhost 1667
```

---

## 2. 作为 .NET 库引用

将 nREPL server 集成到你的 .NET 项目中。

### 方法 A: 复制源码

将 `Program.cs` 中的相关类复制到你的项目中：

```csharp
// 在你的项目中添加以下类:
// - Bencode (编解码)
// - NReplServer (服务器)
// - Session (会话)

// 然后启动服务器
var server = new NReplServer("127.0.0.1", 1667);
server.Start();
```

### 方法 B: 创建类库

将 nREPL server 打包为类库引用：

```bash
# 修改 .csproj 为类库
dotnet new classlib -n ClojureCLR.NRepl -o ./nrepl-lib
cp Program.cs ./nrepl-lib/

# 修改输出类型
# <OutputType>Library</OutputType>

# 打包
dotnet pack -c Release

# 在你的项目中引用
dotnet add package ClojureCLR.NRepl --source ./nrepl-lib/bin/Release
```

### 在应用中启动

```csharp
using clojureCLR_nrepl;

public class MyApplication
{
    private NReplServer _nreplServer;

    public void Start()
    {
        // 启动你的应用...

        // 同时启动 nREPL server
        _nreplServer = new NReplServer("127.0.0.1", 1667);
        _nreplServer.Start();

        Console.WriteLine("应用已启动，nREPL server 在 127.0.0.1:1667");
    }

    public void Stop()
    {
        _nreplServer?.Stop();
    }
}
```

---

## 3. 集成到现有应用

### Unity 游戏引擎

创建 Unity 编辑器脚本：

```csharp
// Assets/Editor/ClojureNReplEditor.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using clojureCLR_nrepl;

[InitializeOnLoad]
public static class ClojureNReplEditor
{
    private static NReplServer _server;
    private const int Port = 1667;

    static ClojureNReplEditor()
    {
        EditorApplication.delayCall += StartServer;
        EditorApplication.quitting += StopServer;
    }

    [MenuItem("Clojure/Start nREPL Server")]
    private static void StartServer()
    {
        if (_server == null)
        {
            _server = new NReplServer("127.0.0.1", Port);
            _server.Start();
            Debug.Log($"nREPL server started on port {Port}");
        }
    }

    [MenuItem("Clojure/Stop nREPL Server")]
    private static void StopServer()
    {
        _server?.Stop();
        _server = null;
        Debug.Log("nREPL server stopped");
    }

    [MenuItem("Clojure/nREPL Status")]
    private static void ShowStatus()
    {
        if (_server != null)
            EditorUtility.DisplayDialog("nREPL Status",
                $"Server running on 127.0.0.1:{Port}", "OK");
        else
            EditorUtility.DisplayDialog("nREPL Status",
                "Server not running", "OK");
    }
}
#endif
```

### ASP.NET Core 应用

```csharp
// Program.cs
using clojureCLR_nrepl;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 启动 nREPL server
var nreplPort = builder.Configuration.GetValue<int>("NRepl:Port", 1667);
var nreplServer = new NReplServer("127.0.0.1", nreplPort);
nreplServer.Start();

app.Lifetime.ApplicationStopping.Register(() => {
    nreplServer.Stop();
});

app.Run();
```

配置 `appsettings.json`:
```json
{
  "NRepl": {
    "Port": 1667,
    "Host": "127.0.0.1"
  }
}
```

### 后台服务 / Windows Service

```csharp
using System.ServiceProcess;
using clojureCLR_nrepl;

public class ClojureNReplService : ServiceBase
{
    private NReplServer _server;

    protected override void OnStart(string[] args)
    {
        _server = new NReplServer("127.0.0.1", 1667);
        _server.Start();
        EventLog.WriteEntry("Clojure nREPL Service started");
    }

    protected override void OnStop()
    {
        _server?.Stop();
    }
}
```

---

## 4. 配置选项

### 命令行参数

修改 `Program.cs` 支持命令行参数：

CLI 使用环境变量配置：

```csharp
var host = Environment.GetEnvironmentVariable("NREPL_HOST") ?? "127.0.0.1";
var port = int.Parse(Environment.GetEnvironmentVariable("NREPL_PORT") ?? "1667");
```

使用：
```bash
NREPL_HOST=0.0.0.0 NREPL_PORT=7888 \
  dotnet run --project cli/clojureCLR-nrepl-cli.csproj
```

### 配置文件

创建 `nrepl-config.json`:

```json
{
  "host": "127.0.0.1",
  "port": 1667,
  "debug": false,
  "maxSessions": 10
}
```

读取：

```csharp
using System.Text.Json;

var config = JsonSerializer.Deserialize<NReplConfig>(
    File.ReadAllText("nrepl-config.json"));

var server = new NReplServer(config.Host, config.Port);
```

---

## 5. 最佳实践

### 自动补全（CIDER/Calva）

1. **CLR 类型静态成员补全**
   已导入类型可直接使用 `Type/Member` 形式补全；也支持完整类型名：
   ```clojure
   (import 'System.Linq.Enumerable)
   (Enumerable/Where ...)
   (System.Linq.Enumerable/Where ...)
   ```
   未导入时不会出现 `Enumerable/*` 补全候选，但 `System.Linq.Enumerable/*` 可用。

2. **实例成员补全（. 形式）**
   支持以下常见写法（接收者需是命名空间中已解析的符号）：
   ```clojure
   (. s Sub)            ; s 为字符串变量
   (.Substring s)       ; method-first
   ```
   如果接收者解析为 `Type`，则会返回该类型的静态成员补全。

3. **补全请求参数**
   - 大多数客户端会发送 `symbol` 或 `prefix`，服务器直接使用。
   - 若客户端只发 `line`/`buffer`/`code` + `column`/`cursor`/`pos`，服务器会自动从当前行提取前缀。
   - 排查补全问题可设置环境变量 `NREPL_DEBUG_COMPLETE=1` 查看请求字段。

4. **已知限制**
   - 仅支持 **public** 成员。
   - `.` 补全目前只支持 **符号接收者**（不解析复杂表达式）。
   - 不做重载参数的智能排序或过滤。

### 符号信息与参数提示

- `info`/`eldoc` 支持 `Type/Member` 的 CLR 静态成员：
  ```clojure
  (Enumerable/Where)
  ; info/eldoc 会返回参数列表与成员类型
  ```

### 安全配置

```csharp
// 仅本地访问（最安全）
var server = new NReplServer("127.0.0.1", 1667);

// 内网访问（需谨慎）
var server = new NReplServer("0.0.0.0", 1667);

// 配合防火墙
// sudo ufw allow from 192.168.1.0/24 to any port 1667
```

### 性能优化

```csharp
// 限制并发会话数
public class NReplServer
{
    private readonly SemaphoreSlim _connectionLimiter =
        new SemaphoreSlim(10); // 最多10个并发连接

    private void HandleClient(TcpClient client)
    {
        if (!_connectionLimiter.Wait(0))
        {
            // 拒绝连接
            client.Close();
            return;
        }

        try
        {
            // 处理客户端...
        }
        finally
        {
            _connectionLimiter.Release();
        }
    }
}
```

### 日志记录

```csharp
using Microsoft.Extensions.Logging;

public class NReplServer
{
    private readonly ILogger<NReplServer> _logger;

    public NReplServer(string host, int port, ILogger<NReplServer> logger = null)
    {
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole())
                                          .CreateLogger<NReplServer>();
        // ...
    }

    private void HandleMessage(...)
    {
        _logger.LogInformation("Processing op: {Op}", op);
        // ...
    }
}
```

### 健康检查

```csharp
// 添加健康检查端点（HTTP）
app.MapGet("/health", () => new {
    status = "ok",
    nrepl = nreplServer.IsRunning
});

// 或者通过 nREPL 本身
// 发送 {"op": "describe"} 检查响应
```

---

## 示例项目

### 最小可行示例

```csharp
// MyApp.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- 复制 Program.cs 到项目 -->
    <Compile Include="../clojureCLR-nrepl/Program.cs" />
    <PackageReference Include="Clojure" Version="1.11.0" />
  </ItemGroup>
</Project>
```

```csharp
// Program.cs (你的应用)
using clojureCLR_nrepl;

Console.WriteLine("Starting my app with nREPL...");

// 启动 nREPL
var nrepl = new NReplServer("127.0.0.1", 1667);
nrepl.Start();

// 你的应用逻辑
while (true)
{
    Console.WriteLine("Working... (press Ctrl+C to stop)");
    Thread.Sleep(5000);
}
```

### Docker 部署

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app
COPY ./publish .

EXPOSE 1667

ENTRYPOINT ["./clojureCLR-nrepl"]
```

```yaml
# docker-compose.yml
version: '3'
services:
  nrepl:
    build: .
    ports:
      - "1667:1667"
    environment:
      - NREPL_HOST=0.0.0.0
```

---

## 故障排除

### 端口冲突

```bash
# 检查端口占用
lsof -i:1667
netstat -an | grep 1667

# 使用其他端口
NREPL_PORT=7888 dotnet run --project cli/clojureCLR-nrepl-cli.csproj
```

### 连接被拒绝

1. 检查防火墙设置
2. 确认 `host` 配置（`127.0.0.1` vs `0.0.0.0`）
3. 验证 ClojureCLR 是否正确加载

### 性能问题

1. 限制并发连接数
2. 启用日志查看慢操作
3. 考虑使用 Unix Domain Socket（未来支持）

---

## 获取帮助

- 查看 [README.md](./README.md) 获取基本信息
- 查看 [CONTRIBUTING.md](./CONTRIBUTING.md) 了解如何贡献
- 提交 Issue 到 GitHub

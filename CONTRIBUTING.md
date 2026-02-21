# 贡献指南

感谢您对 ClojureCLR nREPL Server 的兴趣！

## 开发环境

### 必需工具

- .NET SDK 8.0+
- Python 3.x（用于测试）
- Clojure CLI（可选，用于 lein repl 测试）

### 构建

```bash
# 构建项目
dotnet build

# 运行（CLI）
dotnet run --project cli/clojureCLR-nrepl-cli.csproj

# 发布
dotnet publish -c Release
```

## 代码结构

```
src/
├── BencodeCodec.cs                 # Bencode 编解码
├── NReplServer.Core.cs             # 连接/协议/消息分发
├── NReplServer.Eval.cs             # eval 与命名空间切换
├── NReplServer.SessionHandlers.cs  # clone/ls-sessions/interrupt/load-file
├── NReplServer.Completion.cs       # 补全入口
├── NReplServer.Completion.Dot.cs   # 点调用补全
├── NReplServer.Completion.Cache.cs # 反射缓存
├── NReplServer.Info.cs             # info 处理
├── NReplServer.Eldoc.cs            # eldoc 入口
├── NReplServer.Eldoc.Dot.cs        # 点调用 eldoc
├── NReplServer.Eldoc.Clr.cs        # CLR 静态成员 eldoc
├── NReplServer.Utilities.cs        # 通用工具
├── NReplServer.Parsing.cs          # 补全/eldoc 解析
└── NReplSession.cs                 # 会话模型
```

## 添加新功能

### 1. 添加新的 nREPL 操作

以 `ns-path` 为例：

```csharp
// 1. 在 HandleMessage 中添加路由
case "ns-path":
    HandleNsPath(stream, request, sessionId, useLengthPrefix, session);
    break;

// 2. 实现处理器
private void HandleNsPath(NetworkStream stream, Dictionary<string, object> request,
                          string sessionId, bool useLengthPrefix, Session session)
{
    var id = GetId(request);
    var ns = request.GetValueOrDefault("ns") as string;
    
    try
    {
        var path = GetNamespacePath(ns);
        
        SendResponse(stream, new Dictionary<string, object>
        {
            ["id"] = id,
            ["session"] = sessionId,
            ["path"] = path,
            ["status"] = new List<string> { "done" }
        }, useLengthPrefix);
    }
    catch (Exception e)
    {
        SendResponse(stream, new Dictionary<string, object>
        {
            ["id"] = id,
            ["session"] = sessionId,
            ["status"] = new List<string> { "no-ns" }
        }, useLengthPrefix);
    }
}

// 3. 更新 describe 响应
private Dictionary<string, object> CreateDescribeResponse(string id, string sessionId)
{
    var ops = new Dictionary<string, object>
    {
        // ... 现有操作
        ["ns-path"] = new Dictionary<string, object>()
    };
    // ...
}
```

### 2. 编写测试

在 `test_nrepl.py` 中添加测试用例：

```python
def test_ns_path():
    sock = connect()
    
    # Clone
    send(sock, {"op": "clone", "id": 1})
    session = receive(sock)[0]["new-session"]
    
    # Test ns-path
    send(sock, {"op": "ns-path", "id": 2, "session": session, "ns": "clojure.core"})
    response = receive(sock)[0]
    
    assert "path" in response or "no-ns" in response["status"]
    print("✓ ns-path works")
    
    sock.close()
```

## 调试技巧

### 启用调试日志

修改代码中的日志输出：

```csharp
Console.WriteLine($"Received op: '{op}', id: '{id}'");
Console.WriteLine($"Evaluating: {code}");
Console.WriteLine($"Result: {result}");
```

### 使用 Python 手动测试

```python
import socket

def send(sock, msg):
    # Bencode 编码并发送
    ...

def receive(sock):
    # 接收并 Bencode 解码
    ...

sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
sock.connect(('127.0.0.1', 1667))

# 测试
send(sock, {"op": "clone", "id": 1})
print(receive(sock))
```

### Wireshark 抓包

```bash
# 捕获 loopback 流量
sudo tcpdump -i lo0 -A -s 0 port 1667
```

## 性能优化

### 当前瓶颈

1. **Clojure 加载时间**: 首次 eval 需要 ~3 秒加载 clojure.core
2. **消息解析**: 大消息 Bencode 解析可以优化
3. **内存**: Session 对象不会自动清理

### 优化方向

- [ ] 预加载 Clojure 核心
- [ ] 添加 LRU 会话清理
- [ ] 支持消息流式处理
- [ ] 异步 eval

## 发布流程

1. 更新版本号（AssemblyVersion）
2. 更新 CHANGELOG.md
3. 打标签：`git tag v0.2.0`
4. 构建 Release：`dotnet publish -c Release`
5. 创建 GitHub Release

## 代码规范

- 使用 C# 10 特性
- 遵循 Microsoft 命名规范
- 添加 XML 文档注释（公共 API）
- 保持方法简短（< 50 行）

## 常见问题

### Q: 如何处理 ClojureCLR 的异常？
A: 捕获并转换为 nREPL error 响应：

```csharp
catch (Exception e)
{
    return $"#error {{:message \"{e.Message}\"}}";
}
```

### Q: 如何支持新的 Bencode 类型？
A: 在 `Bencode.EncodeObject` 中添加 case。

### Q: 如何处理大消息？
A: 目前缓冲区为 8KB，对于大结果需要实现流式发送。

## 联系

- 提交 Issue: GitHub Issues
- 讨论: GitHub Discussions

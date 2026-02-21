# 快速开始

## 1. 独立运行（最简单）

```bash
# 下载并运行
git clone <repo-url>
cd clojureCLR-nrepl
dotnet run --project cli/clojureCLR-nrepl-cli.csproj

# 连接 CIDER
M-x cider-connect-clj  # 127.0.0.1:1667
```

可选：Clojure-only 入口（适合作为所有项目起点）

```bash
dotnet tool install --global Clojure.Main --version 1.12.3-alpha4
clojure.main -i demo/run-repl-only.clj -e "(demo.run-repl-only/-main)"
```

## 2. 嵌入 .NET 应用

```csharp
// 添加到你的 Program.cs
using clojureCLR_nrepl;

var nrepl = new NReplServer("127.0.0.1", 1667);
nrepl.Start();

// 你的应用代码...

// 清理时
nrepl.Stop();
```

## 3. 常用命令

```bash
# 构建
make build

# 运行测试
make test

# 发布
make release

# 自定义端口
NREPL_PORT=7888 dotnet run --project cli/clojureCLR-nrepl-cli.csproj
```

## 4. 客户端连接

| 工具      | 命令                                |
|-----------|-------------------------------------|
| CIDER     | `M-x cider-connect-clj`             |
| Calva     | `Calva: Connect to Running REPL`    |
| Leiningen | `lein repl :connect 127.0.0.1:1667` |

## 5. 测试

```bash
# Python 测试
python3 test_nrepl.py

# 手动测试
echo 'd2:op4:eval2:idi1e4:code9:(+ 1 2 3)e' | nc localhost 1667
```

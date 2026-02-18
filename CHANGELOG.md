# Changelog

所有值得注意的变更都将记录在此文件中。

格式基于 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added
- 计划: `ns-path` 操作 - 查找命名空间文件路径
- 计划: `apropos` 操作 - 符号搜索
- 计划: `macroexpand` 操作 - 宏展开
- 计划: `classpath` 操作 - 类路径查询

## [0.1.0] - 2024-02-18

### Added
- 基础 nREPL 服务器实现
  - Bencode 编解码（支持字典、列表、整数、字符串）
  - 长度前缀格式支持
  - 原始 Bencode 格式支持
- 核心 nREPL 操作
  - `eval` - Clojure 代码求值
  - `clone` - 创建新会话
  - `close` - 关闭会话
  - `ls-sessions` - 列出活动会话
  - `describe` - 服务器能力描述
  - `interrupt` - 执行中断
  - `load-file` - 文件加载
  - `stdin` - 标准输入支持
- CIDER Middleware 支持
  - `complete` - 自动补全（支持 namespace 简写如 `str/join`）
  - `info` - 符号信息查询（文档、参数、元数据）
  - `eldoc` - 函数参数提示（minibuffer 显示）
- 会话管理
  - 基于 GUID 的会话标识
  - 跨操作 namespace 保持
  - `in-ns` 特殊处理（自动 refer clojure.core）
- 标准库支持
  - 常见 namespace 简写映射（str→clojure.string, io→clojure.java.io 等）
  - 自动按需加载 namespace
- 测试工具
  - Python 测试脚本
  - 集成测试覆盖主要功能

### Fixed
- Bencode 解析器正确处理空字符串（`0:`）
- `in-ns` 不保持 namespace 的问题
- eldoc 返回 `no-eldoc` 状态码（CIDER 兼容性）
- 大整数 ID 解析（CIDER 使用 `i13e` 格式）

### Known Issues
- 首次 eval 需要 ~3 秒加载 clojure.core
- Session 对象不会自动清理（内存泄漏风险）
- 不支持消息流式处理（大消息可能超时）

## [0.0.1] - 2024-02-15

### Added
- 项目初始化
- 基础 TCP 服务器
- 简单消息回显

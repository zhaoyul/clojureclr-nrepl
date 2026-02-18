# ClojureCLR nREPL Server Makefile

.PHONY: build run test clean release

# 默认目标
all: build

# 构建项目
build:
	dotnet build

# 运行服务器
run:
	dotnet run --project clojureCLR-nrepl.csproj

# 发布 Release
release:
	dotnet publish -c Release -o ./publish

# 运行测试
test: build
	@echo "Starting server..."
	@dotnet run --project clojureCLR-nrepl.csproj &
	@sleep 3
	@echo "Running tests..."
	@python3 test_nrepl.py || true
	@echo "Stopping server..."
	@lsof -ti:1667 | xargs kill -9 2>/dev/null || true

# 快速测试（假设服务器已在运行）
test-quick:
	python3 test_nrepl.py

# 清理构建产物
clean:
	dotnet clean
	rm -rf ./publish
	rm -rf ./bin
	rm -rf ./obj

# 格式化代码
format:
	dotnet format

# 检查代码
lint:
	dotnet build --verbosity quiet 2>&1 | grep -E "(error|warning)" || echo "No issues found"

# 启动测试服务器并后台运行
dev:
	@echo "Starting development server..."
	@dotnet run --project clojureCLR-nrepl.csproj &
	@echo "Server PID: $$!"
	@echo "Connect with: M-x cider-connect-clj (127.0.0.1:1667)"

# 停止测试服务器
stop:
	@lsof -ti:1667 | xargs kill -9 2>/dev/null || echo "No server running"

# 打包发布版本
package: release
	@echo "Creating package..."
	@mkdir -p ./dist
	@tar -czf ./dist/clojureCLR-nrepl-$(shell date +%Y%m%d).tar.gz -C ./publish .
	@echo "Package created in ./dist/"

# 显示帮助
help:
	@echo "Available targets:"
	@echo "  build      - Build the project"
	@echo "  run        - Run the server"
	@echo "  test       - Run all tests (start server, test, stop)"
	@echo "  test-quick - Run tests only (server must be running)"
	@echo "  release    - Build release version"
	@echo "  package    - Create distribution package"
	@echo "  clean      - Clean build artifacts"
	@echo "  dev        - Start server in development mode"
	@echo "  stop       - Stop running server"
	@echo "  format     - Format code"
	@echo "  lint       - Check for warnings/errors"

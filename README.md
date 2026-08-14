# Valency.Shell

一个用 C# / .NET 9 编写的跨平台命令行 shell，支持 Windows、Linux 和 macOS（包括纯终端环境如 Debian 服务器、Android Termux）。

## 特性

- **交互式行编辑**：仿 PowerShell/PSReadLine —— 历史记录（↑/↓）、行内编辑、`Ctrl+A/E/U/K/W`、`Ctrl+L` 清屏、`Ctrl+C` 取消当前行、`Ctrl+D` 空行退出
- **Tab 补全**：对标 pwsh，补全内置命令、PATH 可执行文件、目录/文件；多候选时先补全到最长公共前缀，再按 Tab 循环
- **命令语法**：`;` / `&&` / `||` 命令分隔与短路、`|` 管道（末尾可为内置命令）、`&` 后台作业（Start-Job 风格，输出捕获避免与提示符交错）
- **变量展开**：`$VAR` / `${VAR}` / `$env:VAR`；未定义变量置空；`~` 展开为用户目录；`$?` 为上一条命令退出码
- **语法高亮**：命令（合法蓝/未知红）、字符串（黄）、变量（品红）、分隔符（青）
- **可配置提示符**：默认单行 `user@dir#`，可选 Kali 风格双行；支持 `$USER` `$HOST` `$PWD` `$SHARP` 等变量自定义模板，内置 ANSI 配色
- **结构化日志**：基于 Serilog，写入 `~/.valency/logs/`（按 session 分文件、滚动），UDP 实时推送 + 独立日志查看器

## 内置命令

| 命令 | 说明 |
|---|---|
| `exit [code]` | 退出 shell（可带退出码） |
| `cd [dir]` | 切换目录（无参回家目录，`-` 回上一个目录） |
| `pwd` | 打印当前工作目录 |
| `jobs` | 列出正在运行的后台作业 |
| `logs` | 查看日志（`--tail` `--head` `--level` `--follow`） |
| `prompt` | 查看/切换提示符风格（`plain` / `kali` / `custom`） |
| `grep` | 筛选字符串（`-i` `-v` `-n` `-c`，可接管道） |
| `help [cmd]` | 列出命令，或查看某个命令的详细帮助 |

任意命令加 `--help` 或 `-h` 查看帮助。

## 构建与运行

```bash
# 运行
dotnet run --project Valency.Shell

# 构建 / 测试
dotnet build
dotnet test

# 自包含发布（无需安装 .NET，单文件）
dotnet publish Valency.Shell/Valency.Shell.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

## 配置（环境变量）

| 变量 | 作用 | 默认 |
|---|---|---|
| `VALENCY_PROMPT` | 提示符风格 `plain` / `kali` / `custom` | `plain` |
| `VALENCY_PROMPT_FORMAT` | 自定义提示符模板 | — |
| `VALENCY_LOG_DIR` | 日志目录 | `~/.valency/logs` |
| `VALENCY_LOG_PORT` | UDP 实时日志端口 | `7310` |

自定义提示符模板使用 `$变量` 语法：`$USER`（用户名）、`$HOST` / `$HOSTNAME`（主机名）、`$PWD`（当前目录，用户目录缩写为 `~`）、`$SHARP`（权限符 `#`/`$`）、`$CONN`（连接符 `@`），其余按环境变量展开。例如：

```bash
export VALENCY_PROMPT_FORMAT='[$USER@$PWD] '
```

## 日志

每次启动生成 `~/.valency/logs/session-<时间戳>.log`，10MB 滚动、保留 5 个，内容按级别记录（文件记 Debug 全量，UDP 实时只记 Info 关键事件）。

- `logs`：查看当前会话日志
- `logs --follow`：在独立窗口实时跟随（UDP）
- 日志查看器：`Valency.Shell.LogViewer --udp [port]` 或 `--file <path>`

## 项目结构

```
Valency.Shell.slnx
├── Valency.Shell.Core/       纯逻辑：分词、解析、变量展开、补全、高亮、路径解析
├── Valency.Shell.Engine/     进程执行：Run / RunPipeline / 后台作业
├── Valency.Shell/            Host：REPL、行编辑器、内置命令、提示符、日志
├── Valency.Shell.LogViewer/  日志查看器
└── Valency.Shell.Tests/      xUnit 测试
```

## 许可

见 LICENSE.txt

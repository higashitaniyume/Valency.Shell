# Valency.Shell

一个用 C# / .NET 9 编写的跨平台命令行 shell，支持 Windows、Linux 和 macOS（包括纯终端环境如 Debian 服务器、Android Termux）。

除了交互式 shell，它内建一个**完整的 Lua 解释器**（基于 [MoonSharp](https://www.moonsharp.org/)，纯 C# 实现，无原生依赖）：闭包、元表、协程、变参、多返回值、`string`/`math`/`table`/`os` 标准库一应俱全，任何合法 Lua 程序都能直接运行；`require()` 可加载纯 Lua 库（penlight 等）。

命令以**函数调用**的形式融入 Lua：`git("status")`、`capture("ls")`、`pipe(...)`；核心命令更有**对象化形态**——`ls()` 返回结构化 table、可链式处理（`:filter():map():sort()`）、REPL 自动渲染，仿 PowerShell 的对象管道而对象完全是 Lua 值。

## 特性

- **交互式行编辑**：仿 PowerShell/PSReadLine —— 历史记录（↑/↓）、行内编辑、`Ctrl+A/E/U/K/W`、`Ctrl+L` 清屏、`Ctrl+C` 取消当前行、`Ctrl+D` 空行退出
- **完整 Lua 脚本**：MoonSharp 驱动，语言即标准 Lua；REPL 单表达式自动回显结果
- **命令即函数**：`git("status", "-s")` 直接调用任意 PATH 可执行文件或内置命令（零注册的全局代理）；`run` / `capture` / `pipe` / `spawn` / `glob` 等 shell API
- **对象化命令**：`ls()`/`grep()`/`jobs()` 等返回结构化 Lua table，方法链处理，表格自动渲染（仿 Format-Table/List）
- **Lua 生态**：`require()` 加载纯 Lua 库（`~/.valency/lua`、`VALENCY_LUA_PATH`）
- **Tab 补全**：调用位置补全命令/函数名（补全后自动带 `(`），字符串里补全路径；多候选先补全到最长公共前缀，再按 Tab 循环
- **语法高亮**：Lua 关键字（青）、字符串（黄）、注释（绿）、调用位置（合法命令蓝/未知红）
- **可配置提示符**：默认单行 `user@dir#`，可选 Kali 风格双行；支持 `$USER` `$HOST` `$PWD` `$SHARP` 等变量自定义模板，内置 ANSI 配色
- **结构化日志**：基于 Serilog，写入 `~/.valency/logs/`（按 session 分文件、滚动），UDP 实时推送 + 独立日志查看器

## 构建与运行

```bash
# 运行（交互式 REPL）
dotnet run --project Valency.Shell

# 构建 / 测试
dotnet build
dotnet test

# 自包含发布（无需安装 .NET，单文件）
dotnet publish Valency.Shell/Valency.Shell.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

## 运行脚本

```bash
# 执行脚本文件（剩余参数作为 args[1..n] 传入，args[0] 是脚本名）
valency script.lua arg1 arg2

# 在 shell 内部直接执行脚本（./ 或路径，.lua/.vsh/.sh/.bash/.zsh 都按 Lua 运行）
./demo.lua arg1
scripts/build.lua

# 执行单条命令（类似 lua -e）
valency -c 'for i = 1, 3 do echo(i) end'

# 从标准输入读取脚本
cat script.lua | valency
```

## 脚本语言：Lua + Shell API

语言就是标准 Lua（MoonSharp 提供 Lua 5.x 语义）。REPL 与 `-c` 中，单独一行表达式会自动回显求值结果。

### 命令即函数

```lua
git("status", "-s")            -- 任意 PATH 可执行文件：直接按函数调用，返回退出码
echo("hello", "world")         -- 内置命令同样是函数
local out = capture("ls", "-l")-- 捕获 stdout；第二个返回值是退出码
local out, code = capture("git", "status")

run("make", "-j4")             -- 显式调用，返回退出码
```

未定义的全局名如果能解析为内置命令或 PATH 可执行文件，会自动得到一个命令代理函数——无需注册即可调用；其余名字保持 `nil`。

### 对象化 API：命令返回 Lua table

仿 PowerShell 的对象管道——命令返回**结构化 Lua 值**，REPL 自动渲染（同构 table 数组对齐成表格、单 table 按键值列出），`echo()` 也会渲染：

```lua
ls("src")                       -- {{name=.., path=.., size=.., is_dir=.., mtime=..}, ...}
cat("a.txt")                    -- string
lines("a.log")                  -- {"line1", ...}
grep("error", lines("a.log"))   -- 匹配行数组
writefile("out.txt", "data")    -- true；第三个参数 append=true 追加
jobs()                          -- {{id=1, pid=10892, cmd="make", state="running"}, ...}
```

### 方法链：PS 管道的对应物

命令返回的数组 table 可以链式处理，返回值仍是普通 table（`ipairs`/`#` 照常）：

```lua
ls("src")
  :filter(function(e) return not e.is_dir end)
  :map(function(e) return e.name end)
  :sort()
  :echo()                       -- 渲染输出，终结链（类似 Out-Host）

grep("error", lines("app.log"))
  :reverse()
  :take(10)
  :echo()

ls():sort(function(a, b) return a.mtime > b.mtime end)  -- 自定义比较器
```

可用链：`:filter(pred)`、`:map(fn)`（丢弃 nil 结果）、`:sort([cmp])`、`:reverse()`、`:take(n)`、`:echo()`。

### Lua 生态：require 纯 Lua 库

`require()` 从以下路径解析模块（`?` 占位）：当前目录 `./?.lua`、`./?/init.lua`，用户库目录 `~/.valency/lua/`；环境变量 `VALENCY_LUA_PATH`（分号分隔）可追加。penlight、middleclass 等纯 Lua 库放进去即可使用：

```lua
local strx = require("pl.stringx")
local class = require("middleclass")
```

注：宿主为 MoonSharp（纯 C# 实现），**C 扩展库**（lfs/lpeg/luasocket 等）不可用——文件系统等能力由上面对象化 API 承担。

### 管道与重定向

```lua
pipe("cat a.log", "grep error")                 -- 字符串阶段按空白拆分
pipe({ "cat", "a.log" }, { "grep", "error" })   -- 数组阶段更精确（推荐）

-- 末尾选项表：out / err / append / merge / input
pipe({ "cmd", "/c", "dir" }, { "grep", "txt" }, { out = "out.txt" })
run("make", { out = "build.log", append = true })
run("test", { err = "err.log" })
run("cmd", { out = "all.log", merge = true })   -- 2>&1
run("app", { input = "data.txt" })              -- stdin
```

注：管道的中间阶段必须是外部进程；内置命令（如 `grep`）可作为最后一个阶段。

### 后台作业

```lua
spawn("sleep", "60")   -- 启动后台作业，返回作业号（宿主同时打印 [job] pid）
jobs()                 -- 列出正在运行的作业
```

### 变量与环境

```lua
name = "world"         -- Lua 全局变量（shell 变量即 Lua 变量）
local x = 1 + 2 * 3
env.PATH               -- 读环境变量
env.MY_FLAG = "1"      -- 写环境变量（直写进程环境）
export("EDITOR=vim")   -- 内置命令同样可用
read("answer")         -- 从 stdin 读一行到 Lua 全局 answer
status()               -- 上一条命令的退出码
args[0], args[1], #args -- 脚本名 / 位置参数 / 参数个数
```

### 完整 Lua

```lua
local t = setmetatable({}, { __index = function(_, k) return k .. "!" end })
local co = coroutine.wrap(function() coroutine.yield(1) return 2 end)

local function counter()
	local n = 0
	return function() n = n + 1 return n end
end

for _, f in ipairs(glob("*.cs")) do   -- glob 返回匹配表
	echo(f)
end

if x > 3 and s ~= "" then echo("ok") end

exit(0)                                -- 显式退出
```

## 内置命令

内置命令以函数形式调用（`cd("/tmp")`），任意命令加 `--help`/`-h` 查看帮助。

| 命令 | 说明 |
|---|---|
| `exit [code]` | 退出 shell（可带退出码） |
| `cd [dir]` | 切换目录（无参回家目录，`-` 回上一个目录） |
| `pwd` | 打印当前工作目录 |
| `echo [-n] [-e] [text...]` | 输出一行文本 |
| `test` / `[ ... ]` | 条件测试（`-eq -ne -lt -le -gt -ge = != -z -n -f -d -e`） |
| `true` / `false` / `:` | 恒成功 / 恒失败 / 空操作 |
| `export [NAME[=VALUE]...]` | 导出变量到子进程环境 |
| `unset NAME...` | 删除变量 |
| `read NAME...` | 从标准输入读取一行并赋值 |
| `shift [n]` | 左移位置参数 |
| `source` / `. FILE` | 读取并执行脚本文件 |
| `jobs` | 列出正在运行的后台作业 |
| `grep` | 筛选字符串（`-i` `-v` `-n` `-c`，可接管道） |
| `logs` | 查看日志（`--tail` `--head` `--level` `--follow`） |
| `prompt` | 查看/切换提示符风格（`plain` / `kali` / `custom`） |
| `help [cmd]` | 列出命令，或查看某个命令的详细帮助 |

`break` / `continue` / `return` 是 Lua 原生语句，不再作为内置命令。

## 配置（环境变量）

| 变量 | 作用 | 默认 |
|---|---|---|
| `VALENCY_PROMPT` | 提示符风格 `plain` / `kali` / `custom` | `plain` |
| `VALENCY_PROMPT_FORMAT` | 自定义提示符模板 | — |
| `VALENCY_LOG_DIR` | 日志目录 | `~/.valency/logs` |
| `VALENCY_LOG_PORT` | UDP 实时日志端口 | `7310` |
| `VALENCY_LOG_LEVEL` | 日志级别，`verbose` 时输出语句级详情（文件与 UDP 实时都可见） | `debug` |

自定义提示符模板使用 `$变量` 语法：`$USER`（用户名）、`$HOST` / `$HOSTNAME`（主机名）、`$PWD`（当前目录，用户目录缩写为 `~`）、`$SHARP`（权限符 `#`/`$`）、`$CONN`（连接符 `@`），其余按环境变量展开。例如：

```bash
export VALENCY_PROMPT_FORMAT='[$USER@$PWD] '
```

## 日志

每次启动生成 `~/.valency/logs/session-<时间戳>.log`，10MB 滚动、保留 5 个，内容按级别记录（文件记 Debug 全量，UDP 实时只记 Info 关键事件）。设置 `VALENCY_LOG_LEVEL=verbose` 后，文件与 UDP 实时都会记录语句级 Verbose 日志，可用日志查看器实时查看。

- `logs`：查看当前会话日志
- `logs --level verbose|debug|info|warn|error|fatal`：按级别筛选
- `logs --follow`：在独立窗口实时跟随（UDP）
- 日志查看器：`Valency.Shell.LogViewer --udp [port]` 或 `--file <path>`

## 项目结构

```
Valency.Shell.slnx
├── Valency.Shell.Core/       纯逻辑：路径解析、补全、高亮、内置命令名
├── Valency.Shell.Scripting/  Lua 语言层：LuaShell + shell/对象 API + 渲染器 + 方法链（MoonSharp）、glob
├── Valency.Shell.Engine/     进程执行：Run / RunPipeline / 捕获 / 后台作业
├── Valency.Shell/            Host：REPL、行编辑器、内置命令、提示符、日志
├── Valency.Shell.LogViewer/  日志查看器
└── Valency.Shell.Tests/      xUnit 测试（按 Core/Scripting/Builtins/Engine/Host 分层）
```

## 许可

见 LICENSE.txt

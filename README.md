# Valency.Shell

一个用 C# / .NET 9 编写的跨平台命令行 shell，支持 Windows、Linux 和 macOS（包括纯终端环境如 Debian 服务器、Android Termux）。

除了交互式 shell，它内建一个 **类 C / pwsh 风格的脚本解释器**：词法分析 → 语法树（AST）→ 解释执行，支持 `{}` 代码块、`()` 表达式（C 运算符优先级）、控制流、函数、算术、命令替换与文件重定向。

## 特性

- **交互式行编辑**：仿 PowerShell/PSReadLine —— 历史记录（↑/↓）、行内编辑、`Ctrl+A/E/U/K/W`、`Ctrl+L` 清屏、`Ctrl+C` 取消当前行、`Ctrl+D` 空行退出
- **Tab 补全**：对标 pwsh，补全内置命令、PATH 可执行文件、目录/文件；多候选时先补全到最长公共前缀，再按 Tab 循环
- **脚本解释器**：基于语法树的解释执行，见下文「脚本语言」
- **命令语法**：`;` / `&&` / `||` 命令分隔与短路、`|` 管道（末尾可为内置命令）、`&` 后台作业
- **变量展开**：`$VAR` / `${VAR}` / `$env:VAR`；未定义变量置空；`~` 展开为用户目录
- **语法高亮**：命令（合法蓝/未知红）、字符串（黄）、变量（品红）、分隔符（青）
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
# 执行脚本文件（剩余参数作为 $1..$n 传入）
valency script.sh arg1 arg2

# 执行单条命令（类似 bash -c）
valency -c 'for i in 1 2 3; do echo $i; done'

# 从标准输入读取脚本
cat script.sh | valency
```

## 脚本语言

脚本采用**类 C / pwsh 风格**：大括号 `{}` 包裹代码块，小括号 `()` 放置表达式，表达式遵循 C 运算符优先级。

### 列表与连接符

```bash
echo a; echo b          # ; 顺序执行
mkdir d && cd d         # && 前者成功才执行后者
cd d || echo "失败"     # || 前者失败才执行后者
cat a.txt | grep foo    # 管道（字节流）
sleep 5 &               # 后台执行
! false                 # 命令取反
```

### 控制流

```bash
if ($x == 1) {
    echo "one"
} else if ($x == 2) {
    echo "two"
} else {
    echo "other"
}

$i = 0
while ($i < 10) {
    echo $i
    $i = $i + 1
}

until ($i >= 10) {
    $i = $i + 1
}

for ($i = 0; $i < 10; $i++) {
    echo $i
}
```

### 函数

```bash
function greet($name) {
    echo "hello, $name"
    return 0
}
greet world
```

函数内位置参数仍是 `$1` `$2` …，具名参数映射到 `$1` 起的位置参数。

### 表达式

表达式是原生的一等公民，出现在 `if (...)`、`while (...)`、`for (...)`、赋值右值与 `return` 中：

```bash
$x = 1 + 2 * 3          # 赋值，$x == 7
$y = "ab" + 3           # 字符串拼接，$y == "ab3"
$x += 1                 # 复合赋值
$x++                    # 自增
if ($x > 3 && $y == 5)  # 比较 + 逻辑
$max = ($a > $b) ? $a : $b   # 三元
```

运算符按 C 优先级从低到高：`=` 及复合赋值 → `?:` → `||` → `&&` → `|` → `^` → `&` → `== !=` → `< <= > >=` → `<< >>` → `+ -` → `* / %` → 一元 `! ~ - +` / 前后缀 `++ --` → 字面量。

字面量：整数（含 `0x`）、`"字符串"` / `'字符串'`、`$变量`、`${变量}`、`$(命令)`、`true` / `false`。

### 命令替换

```bash
echo $(ls)              # 捕获命令输出
echo "当前目录：$(pwd)"
```

### 变量与位置参数

```bash
$name = "world"
echo $name              # $VAR
echo ${name}s           # ${VAR} 区分边界
echo $?                 # 上一条命令退出码
echo $$                 # 当前进程 PID
echo $0 $1 $2           # 脚本名与位置参数
echo $#                 # 参数个数
echo $env:PATH          # 环境变量
```

### 文件名展开（glob）

```bash
echo *.cs
rm *.tmp
cp src/*.c dst/
```

支持 `*`、`?`、`[...]`。

### 文件重定向

```bash
cmd > file              # 覆盖写 stdout
cmd >> file             # 追加写 stdout
cmd 2> err              # stderr 到文件
cmd 2>&1                # stderr 合并到 stdout
cmd &> file             # stdout + stderr 到同一文件
cmd < file              # 从文件读 stdin
```

## 内置命令

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
| `break` / `continue` / `return [n]` | 循环 / 函数控制流 |
| `jobs` | 列出正在运行的后台作业 |
| `grep` | 筛选字符串（`-i` `-v` `-n` `-c`，可接管道） |
| `logs` | 查看日志（`--tail` `--head` `--level` `--follow`） |
| `prompt` | 查看/切换提示符风格（`plain` / `kali` / `custom`） |
| `help [cmd]` | 列出命令，或查看某个命令的详细帮助 |

任意命令加 `--help` 或 `-h` 查看帮助。

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
├── Valency.Shell.Core/       纯逻辑：变量展开、路径解析、补全、高亮、内置命令名
├── Valency.Shell.Scripting/  脚本解释器：词法、AST、解析器、算术、词展开、解释执行
├── Valency.Shell.Engine/     进程执行：Run / RunPipeline / 重定向 / 后台作业
├── Valency.Shell/            Host：REPL、行编辑器、内置命令、提示符、日志
├── Valency.Shell.LogViewer/  日志查看器
└── Valency.Shell.Tests/      xUnit 测试（按 Core/Scripting/Builtins/Engine/Host 分层）
```

## 许可

见 LICENSE.txt

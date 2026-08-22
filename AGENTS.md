# AGENTS.md

Valency.Shell 的开发约定与经验，供 AI agent 参考。

## 项目概览

C# / .NET 9 跨平台命令行 shell，内建**完整 Lua 解释器**（MoonSharp 2.0，纯 C#/netstandard，无原生依赖）+ shell 函数 API。语言就是标准 Lua（闭包/元表/协程/变参/多返回值都可用），命令以函数调用形式融入：`git("status")`、`capture("ls")`、`pipe(...)`。

## 项目结构

```
Valency.Shell.Core/       纯逻辑：PathResolver、Highlighter(Lua 规则)、CompletionEngine(调用位置)、BuiltinNames
                          （VariableExpander/IVariableSource 仅提示符模板在用）
Valency.Shell.Scripting/  Lua 语言层：Lua/LuaShell + ILuaHost + LuaMarshaling + ObjectApi(对象化命令)
                          + LuaRenderer(表格渲染) + LuaQuery(方法链)；Expansion/GlobExpander(glob)
Valency.Shell.Engine/     进程执行：ProcessRunner（外部进程、管道、捕获、重定向、后台作业）
Valency.Shell/            Host：Shell(实现 IShellContext+ILuaHost)、LineEditor、Builtins、Prompting、Logging
Valency.Shell.LogViewer/  日志查看器
Valency.Shell.Tests/      xUnit，按 Core/Scripting/Builtins/Engine/Host 分文件夹
```

## Lua 语言层（关键！）

- **MoonSharp 只在 Scripting 项目引用**；宿主通过 `ILuaHost`（Run/Capture/Pipeline/CapturePipeline/Spawn/IsCommandAvailable/RequestExit…）被 Lua 调用，类型不跨 MoonSharp 边界（Shell.cs 不 using MoonSharp）。
- **命令即函数**：全局表挂 `__index` 元方法，名字能被「内置命令/脚本文件/PATH」解析时返回命令代理（有缓存），否则 nil（防 truthiness 陷阱）。
- **shell API**：`run`（返回退出码）、`capture`（返回 stdout+退出码）、`pipe`（阶段为字符串按空白拆或字符串数组；末尾选项表 `out/err/append/merge/input` → LuaRedirect）、`spawn`/`jobs`、`glob`、`exit`、`status`、`env`（代理进程环境）、`args`（0=脚本名，1..n=位置参数）。
- **REPL 回显**：`Execute` 先试 `return (line)` 编译——单表达式行回显结果；命令类调用（run/代理/pipe/spawn）置 `_suppressEcho` 抑制退出码回显。显式 `return` 开头的行/多行块、脚本文件都不回显。
- **exit 流程**：`exit(code)` 回调抛内部 `ExitRequestedException` 跨 MoonSharp 边界，`CallChunk` 同时捕裸异常与 `ScriptRuntimeException.InnerException` 两种形态；命令路径则由 `ThrowIfExitRequested` 兜底。
- **shell 变量 = Lua 全局**；`export/unset/read` 内置经 IShellContext 桥到 Lua 全局 + `Environment`。
- **管道限制**：中间阶段必须是外部进程；内置命令只能做最后一个 stage（其输出经 `PipelineInput` 注入）。
- **capture 实现分叉**：内置/脚本走 `Console.SetOut/Error` 换流；外部进程走 `ProcessRunner.RunPipelineCaptured`。
- **对象化 API（ObjectApi）**：`ls/cat/lines/writefile/grep/jobs` 返回**纯 Lua 值**（table/string），红线是不向 Lua 泄漏 CLR 对象；相对路径以 `ILuaHost.CurrentDirectory` 为基准并支持 `~`。
- **方法链（LuaQuery）**：给返回 table 挂共享 metatable（`__index` → 方法表：filter/map/sort/reverse/take/echo）；对象仍是普通 table。`:echo()` 返回 nil（仿 Out-Host，防 REPL 二次回显）；`map` 丢弃 nil 保持数组稠密。
- **渲染器（LuaRenderer）**：同构 table 数组→对齐表格（数字列右对齐，单元格 48 字符/100 行截断）、单 map→key : value、标量数组→逐行；REPL 回显与原生 `echo()` 都走它。
- **echo 双形态**：原生 `echo(...)` 全局函数渲染参数（遮蔽命令代理）；argv 形态 `run("echo", ...)` 仍走内置命令。
- **require/生态**：`FileSystemScriptLoader`，模块路径 `./?.lua`、`./?/init.lua`、`~/.valency/lua/...`、`VALENCY_LUA_PATH`（分号分隔，无 `?` 自动补 `/?.lua`）；**C 扩展库（lfs/lpeg/luasocket）不可用**（MoonSharp 纯托管），文件系统能力由对象 API 承担。

## i18n

- **铁律：一旦新建任何面向用户的字符串（错误消息、帮助文本、日志模板、提示等），必须放进资源文件，禁止硬编码到代码里。**
- 所有面向用户的字符串放各项目 `Properties/Resources.resx`（默认中文）+ `Resources.en.resx`（英文卫星）。
- 每项目配一个强类型 `Resources.cs`（`internal static`，`ResourceManager` 基名 = `{命名空间}.Properties.Resources`）。
- Console 消息用 `{0}` 占位 + `string.Format(Resources.X, ...)`；**Serilog 日志模板用 `{Name}` 命名占位** + `logger.X(Resources.Y, ...)`，两者别混。
- 加语言 = 加 `Resources.<culture>.resx`，无需改代码。
- resx 里 `<`/`>` 要转义成 `&lt;`/`&gt;`。

## 日志

- Serilog 双 sink：文件（Debug，`VALENCY_LOG_LEVEL=verbose` 时 Verbose）+ UDP 实时（Info，verbose 时 Verbose，供 LogViewer）。
- 组件用 `ForContext("Src", "xxx")` 标来源：`shell`/`proc`/`lua`；输出模板含 `[{Src}]`。
- 分级：Debug=Lua chunk 执行；Info=命令完成耗时/作业/会话。

## 关键坑（踩过的，务必注意）

1. **MoonSharp 的 `Script.LoadString(code, globalTable, friendlyName)`** 第二参是 Table 不是字符串，chunk 名放第三参。
2. **命令类调用的回显抑制**：不抑制的话 REPL 里 `echo("hi")` 会多打一行退出码 `0`。
3. **Lua 字符串里 Windows 路径反斜杠是转义符**：`run("cmd", "/c", "echo", "hi there")` 没问题（参数是独立字符串），但把 `C:\Users\...` 直接嵌进 Lua 字符串会坏——用 `/` 或 `\\`。
4. **`.NET ArgumentList` 给含空格参数加引号**，`cmd /c echo "a b"` 会把引号回显出来（Windows 伪影，非 bug）。
5. **管道阶段传字符串 vs 数组**：`pipe("cmd", "/c", ...)` 每个**字符串参数是一个阶段**（"cmd"、" /c" 各算一段）；多参数命令必须用数组 `pipe({"cmd","/c",...})`。
6. **内置命令写死 `Console.Out/Error`**，capture 靠换流实现——改内置命令时别引入别的输出通道。
7. **`exit` 内置不再抛异常**（ControlFlow 契约已删）：它只 `RequestExit`，Lua 层靠 ExitRequested 中止 chunk。
8. **`#args` 只数 1..n**（Lua 数组部分），`args[0]` 是字符串键单独存。
9. **metatable 的方法必须挂在 `__index` 下**（函数或 table），直接放在 metatable 上的键不会被缺键查找命中——方法链第一版踩过（`attempt to call a nil value`）。
10. **`DynValue` 无公共构造器**：包装现成 `Table` 用 `DynValue.NewTable(script)` 再 `.Table` 取回，保留外层 DynValue 供 `meta.Set("__index", value)` 使用。

## 构建 / 测试

```bash
dotnet build
dotnet test   # xUnit，131 个测试
dotnet run --project Valency.Shell                # 交互 REPL
dotnet run --project Valency.Shell -- sample/demo.lua a b
dotnet run --project Valency.Shell -- -c 'ls():filter(function(e) return e.is_dir end):echo()'
```

## 环境注意

- **VS 2022 开着会乱改项目文件**：会往 csproj/resx 塞 `Resources.Designer.cs`、`ResXFileCodeGenerator`、`<Compile Update>` 条目，注意清理（本仓库用手写 `Resources.cs`，不用 VS 资源设计器）。
- 源码里的中文是 UTF-8，终端显示可能乱码，但文件内容正确。
- 提交用 GPG 签名（`commit.gpgsign=true`）；推送走 `gh` 凭据（token 可能失效，需 `gh auth login`）。
- 仓库行尾统一 LF（`core.autocrlf=true`，2026-08 已归一化），别把 CRLF 提交进去。

-- Valency.Shell Lua 脚本示例
-- 运行：valency sample/demo.lua arg1 arg2
-- 语言就是标准 Lua；命令以函数形式调用。

-- 算术与字符串拼接
local x = 1 + 2 * 3          -- 7
local s = "ab" .. 3          -- "ab3"
echo("x = " .. x .. ", s = " .. s)

-- 位置参数：args[0] 是脚本名，args[1..n] 是参数，#args 是参数个数
echo("script:", args[0], "count:", #args)
if #args > 0 then
	echo("first arg:", args[1])
end

-- 条件与循环
if x > 5 then
	echo("big")
else
	echo("small")
end

for i = 1, 3 do
	echo("loop " .. i)
end

-- 函数与闭包
local function greet(name)
	return "hello, " .. name
end
echo(greet("Valency"))

local function counter()
	local n = 0
	return function()
		n = n + 1
		return n
	end
end
local bump = counter()
bump()
bump()
echo("counted: " .. bump()) -- 3

-- table 与标准库
local t = { name = "valency", tags = { "shell", "lua" } }
echo(t.name, #t.tags, table.concat(t.tags, ","))

-- 命令即函数：退出码是返回值
local code = run("echo", "commands are functions")
echo("exit code: " .. code)

-- 命令替换：capture 返回 stdout 与退出码
local out, c = capture("echo", "captured!")
echo("captured: " .. out .. " (code " .. c .. ")")

-- 管道与重定向（选项表：out / err / append / merge / input）
pipe({ "cmd", "/c", "echo", "hello" }, { "grep", "hello" })

-- glob 文件名展开
for _, f in ipairs(glob("*.md")) do
	echo("found: " .. f)
end

-- 环境变量读写
env.VALENCY_DEMO = "yes"
echo("env = " .. env.VALENCY_DEMO)
env.VALENCY_DEMO = nil

-- 上一条命令的退出码
echo("status = " .. status())

-- 显式退出
exit(0)

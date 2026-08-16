# Valency.Shell 示例脚本
# 用法: valency sample/demo.vsh [名字]

echo "=== 变量与表达式 ==="
$x = 1 + 2 * 3
echo "1 + 2 * 3 = $x"

$greeting = "hello" + ", " + "world"
echo $greeting

echo "=== 位置参数 ==="
echo "脚本名: $0"
echo "参数个数: $#"
echo "第一个参数: $1"

echo "=== if / else ==="
if ($x > 6) {
    echo "x 大于 6"
} else {
    echo "x 不大于 6"
}

echo "=== for 循环 ==="
for ($i = 0; $i < 5; $i++) {
    echo "  i = $i"
}

echo "=== while 循环 ==="
$n = 0
while ($n < 3) {
    echo "  n = $n"
    $n = $n + 1
}

echo "=== 函数 ==="
function greet($name) {
    echo "你好，$name！"
}
greet "Valency"

function add($a, $b) {
    return $a + $b
}
add 3 4
echo "3 + 4 = $?"

echo "=== 命令替换 ==="
$now = $(date)
echo "现在: $now"

echo "=== 三元与比较 ==="
$max = ($x > 10) ? $x : 10
echo "max = $max"

echo "=== 脚本结束 ==="

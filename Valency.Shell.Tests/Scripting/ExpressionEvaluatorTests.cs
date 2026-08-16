using Valency.Shell;

namespace Valency.Shell.Tests.Scripting;

public class ExpressionEvaluatorTests
{
    private static Value Eval(string expression, Func<string, Value>? get = null, Action<string, Value>? set = null)
        => ExpressionEvaluator.Evaluate(expression, get ?? (_ => Value.Int(0)), set);

    private static long Int(string expression) => Eval(expression).AsInt();

    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("10 / 3", 3)]
    [InlineData("10 % 3", 1)]
    [InlineData("1 << 3", 8)]
    [InlineData("8 >> 1", 4)]
    [InlineData("5 & 3", 1)]
    [InlineData("5 | 2", 7)]
    [InlineData("5 ^ 1", 4)]
    [InlineData("-5 + 10", 5)]
    [InlineData("2 * 3 % 4", 2)]
    public void Arithmetic_ProducesResult(string expression, long expected)
    {
        Assert.Equal(expected, Int(expression));
    }

    [Theory]
    [InlineData("5 > 3", true)]
    [InlineData("5 < 3", false)]
    [InlineData("3 == 3", true)]
    [InlineData("3 != 3", false)]
    [InlineData("3 <= 3", true)]
    [InlineData("5 >= 6", false)]
    public void Comparison_ProducesBool(string expression, bool expected)
    {
        Assert.Equal(expected, Eval(expression).Truthy);
    }

    [Theory]
    [InlineData("3 && 4", true)]
    [InlineData("0 && 4", false)]
    [InlineData("0 || 7", true)]
    [InlineData("0 || 0", false)]
    [InlineData("!0", true)]
    [InlineData("!5", false)]
    public void Logical_ProducesBool(string expression, bool expected)
    {
        Assert.Equal(expected, Eval(expression).Truthy);
    }

    [Theory]
    [InlineData("1 ? 10 : 20", 10)]
    [InlineData("0 ? 10 : 20", 20)]
    public void Ternary_ProducesResult(string expression, long expected)
    {
        Assert.Equal(expected, Int(expression));
    }

    [Fact]
    public void StringConcat()
    {
        Assert.Equal("ab3", Eval("\"ab\" + 3").AsString());
    }

    [Fact]
    public void StringComparison()
    {
        Assert.True(Eval("\"b\" > \"a\"").Truthy);
        Assert.True(Eval("\"abc\" == \"abc\"").Truthy);
    }

    [Fact]
    public void VariableLookup()
    {
        Assert.Equal(42, Eval("$x + 1", name => name == "x" ? Value.Int(41) : Value.Int(0)).AsInt());
    }

    [Fact]
    public void Assignment()
    {
        Value captured = Value.Int(0);
        Eval("$x = 5 + 3", null, (_, v) => captured = v);
        Assert.Equal(8, captured.AsInt());
    }

    [Fact]
    public void CompoundAssign()
    {
        Value captured = Value.Int(0);
        Eval("$x += 4", name => Value.Int(10), (_, v) => captured = v);
        Assert.Equal(14, captured.AsInt());
    }

    [Fact]
    public void PreIncrement()
    {
        Value captured = Value.Int(0);
        Eval("++$x", name => Value.Int(5), (_, v) => captured = v);
        Assert.Equal(6, captured.AsInt());
    }

    [Fact]
    public void CommandSubstitution()
    {
        var value = ExpressionEvaluator.Evaluate("$(echo hi)", _ => Value.Int(0), null, _ => "hi");
        Assert.Equal("hi", value.AsString());
    }
}

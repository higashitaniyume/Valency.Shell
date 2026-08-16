using Valency.Shell;

namespace Valency.Shell.Tests.Scripting;

public class ArithmeticTests
{
    private static long Eval(string expression, Func<string, long>? get = null, Action<string, long>? set = null)
        => ArithmeticEvaluator.Evaluate(expression, get ?? (_ => 0), set);

    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("10 / 3", 3)]
    [InlineData("10 % 3", 1)]
    [InlineData("1 << 3", 8)]
    [InlineData("8 >> 1", 4)]
    [InlineData("5 > 3", 1)]
    [InlineData("5 < 3", 0)]
    [InlineData("3 == 3", 1)]
    [InlineData("3 != 3", 0)]
    [InlineData("1 ? 10 : 20", 10)]
    [InlineData("0 ? 10 : 20", 20)]
    [InlineData("3 && 4", 1)]
    [InlineData("0 && 4", 0)]
    [InlineData("0 || 7", 1)]
    [InlineData("5 & 3", 1)]
    [InlineData("5 | 2", 7)]
    [InlineData("5 ^ 1", 4)]
    [InlineData("!0", 1)]
    [InlineData("!5", 0)]
    [InlineData("-5 + 10", 5)]
    [InlineData("2 * 3 % 4", 2)]
    public void Evaluate_ProducesResult(string expression, long expected)
    {
        Assert.Equal(expected, Eval(expression));
    }

    [Fact]
    public void Evaluate_ResolvesVariables()
    {
        Assert.Equal(42, Eval("x + 1", name => name == "x" ? 41 : 0));
    }

    [Fact]
    public void Evaluate_AssignsVariables()
    {
        long captured = 0;
        Eval("x = 5 + 3", null, (_, v) => captured = v);
        Assert.Equal(8, captured);
    }

    [Fact]
    public void Evaluate_CompoundAssign()
    {
        long captured = 0;
        Eval("x += 4", name => 10, (_, v) => captured = v);
        Assert.Equal(14, captured);
    }

    [Fact]
    public void Evaluate_PreIncrement()
    {
        long captured = 0;
        Eval("++x", name => 5, (_, v) => captured = v);
        Assert.Equal(6, captured);
    }
}

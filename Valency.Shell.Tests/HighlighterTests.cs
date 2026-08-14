using Valency.Shell;

namespace Valency.Shell.Tests;

public class HighlighterTests
{
    private static Highlighter Create(params string[] validCommands)
    {
        var set = new HashSet<string>(validCommands, StringComparer.OrdinalIgnoreCase);
        return new Highlighter(name => set.Contains(name));
    }

    [Fact]
    public void Highlight_ValidCommand_IsBlue()
    {
        var spans = Create("git").Highlight("git status");
        Assert.Contains(spans, s => s.Start == 0 && s.Length == 3 && s.Color == ConsoleColor.Blue);
    }

    [Fact]
    public void Highlight_UnknownCommand_IsRed()
    {
        var spans = Create("git").Highlight("nope arg");
        Assert.Contains(spans, s => s.Start == 0 && s.Length == 4 && s.Color == ConsoleColor.Red);
    }

    [Fact]
    public void Highlight_LeadingWhitespace_CommandSpanSkipsIt()
    {
        var spans = Create("git").Highlight("   git");
        Assert.Contains(spans, s => s.Start == 3 && s.Length == 3 && s.Color == ConsoleColor.Blue);
    }

    [Fact]
    public void Highlight_QuotedArgument_IsYellow()
    {
        var spans = Create("echo").Highlight("echo \"hello world\"");
        Assert.Contains(spans, s => s.Start == 5 && s.Length == 13 && s.Color == ConsoleColor.Yellow);
    }

    [Fact]
    public void Highlight_UnclosedQuote_HighlightsToEnd()
    {
        var spans = Create("echo").Highlight("echo \"abc");
        Assert.Contains(spans, s => s.Start == 5 && s.Length == 4 && s.Color == ConsoleColor.Yellow);
    }

    [Fact]
    public void Highlight_EmptyInput_NoSpans()
    {
        Assert.Empty(Create("git").Highlight("   "));
    }

    [Fact]
    public void Highlight_Variable_IsMagenta()
    {
        var spans = Create("echo").Highlight("echo $PATH");
        Assert.Contains(spans, s => s.Start == 5 && s.Length == 5 && s.Color == ConsoleColor.Magenta);
    }

    [Fact]
    public void Highlight_VariableInsideQuotes_MagentaWinsOverYellow()
    {
        var spans = Create("echo").Highlight("echo \"a$PATH\"");
        Assert.Contains(spans, s => s.Start == 7 && s.Length == 5 && s.Color == ConsoleColor.Magenta);
        Assert.Contains(spans, s => s.Color == ConsoleColor.Yellow && s.Start == 5 && s.Length == 2);
    }

    [Fact]
    public void Highlight_VariableInSingleQuotes_NotHighlighted()
    {
        var spans = Create("echo").Highlight("echo '$PATH'");
        Assert.DoesNotContain(spans, s => s.Color == ConsoleColor.Magenta);
        Assert.Contains(spans, s => s.Start == 5 && s.Length == 7 && s.Color == ConsoleColor.DarkYellow);
    }

    [Fact]
    public void Highlight_EnvPrefix_IsMagenta()
    {
        var spans = Create("echo").Highlight("echo $env:PATH");
        Assert.Contains(spans, s => s.Start == 5 && s.Length == 9 && s.Color == ConsoleColor.Magenta);
    }

    [Fact]
    public void Highlight_Separators_AreDarkCyan()
    {
        var spans = Create("echo").Highlight("a && b || c; d");
        Assert.Contains(spans, s => s.Start == 2 && s.Length == 2 && s.Color == ConsoleColor.DarkCyan);
        Assert.Contains(spans, s => s.Start == 7 && s.Length == 2 && s.Color == ConsoleColor.DarkCyan);
        Assert.Contains(spans, s => s.Start == 11 && s.Length == 1 && s.Color == ConsoleColor.DarkCyan);
    }

    [Fact]
    public void Highlight_SeparatorInsideQuotes_NotHighlighted()
    {
        var spans = Create("echo").Highlight("echo \"a;b\"");
        Assert.DoesNotContain(spans, s => s.Color == ConsoleColor.DarkCyan);
    }

    [Fact]
    public void Highlight_EscapedDollar_NotHighlighted()
    {
        var spans = Create("echo").Highlight("echo \\$PATH");
        Assert.DoesNotContain(spans, s => s.Color == ConsoleColor.Magenta);
    }
}

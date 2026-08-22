namespace Valency.Shell.Tests.Core;

public class HighlighterTests
{
	private static Highlighter Create(params string[] validCommands)
	{
		var set = new HashSet<string>(validCommands, StringComparer.OrdinalIgnoreCase);
		return new Highlighter(name => set.Contains(name));
	}

	[Fact]
	public void Highlight_ValidCall_IsBlue()
	{
		var spans = Create("git").Highlight("git(\"status\")");
		Assert.Contains(spans, s => s.Start == 0 && s.Length == 3 && s.Color == ConsoleColor.Blue);
	}

	[Fact]
	public void Highlight_UnknownCall_IsRed()
	{
		var spans = Create("git").Highlight("nope(1)");
		Assert.Contains(spans, s => s.Start == 0 && s.Length == 4 && s.Color == ConsoleColor.Red);
	}

	[Fact]
	public void Highlight_LeadingWhitespace_CallStillColored()
	{
		var spans = Create("git").Highlight("   git(\"\")");
		Assert.Contains(spans, s => s.Start == 3 && s.Length == 3 && s.Color == ConsoleColor.Blue);
	}

	[Fact]
	public void Highlight_Keyword_IsCyan()
	{
		var spans = Create().Highlight("if x then");
		Assert.Contains(spans, s => s.Start == 0 && s.Length == 2 && s.Color == ConsoleColor.Cyan);
		Assert.Contains(spans, s => s.Start == 5 && s.Length == 4 && s.Color == ConsoleColor.Cyan);
	}

	[Fact]
	public void Highlight_PlainIdentifier_Uncolored()
	{
		var spans = Create("git").Highlight("x = foo");
		Assert.Empty(spans);
	}

	[Fact]
	public void Highlight_DoubleQuotedString_IsYellow()
	{
		var spans = Create("echo").Highlight("echo(\"hi\")");
		Assert.Contains(spans, s => s.Start == 5 && s.Length == 4 && s.Color == ConsoleColor.Yellow);
	}

	[Fact]
	public void Highlight_SingleQuotedString_IsYellow()
	{
		var spans = Create("echo").Highlight("echo('hi')");
		Assert.Contains(spans, s => s.Start == 5 && s.Length == 4 && s.Color == ConsoleColor.Yellow);
	}

	[Fact]
	public void Highlight_LongString_IsYellow()
	{
		var spans = Create().Highlight("s = [[abc]]");
		Assert.Contains(spans, s => s.Start == 4 && s.Length == 7 && s.Color == ConsoleColor.Yellow);
	}

	[Fact]
	public void Highlight_UnclosedString_HighlightsToEnd()
	{
		var spans = Create("echo").Highlight("echo(\"abc");
		Assert.Contains(spans, s => s.Start == 5 && s.Length == 4 && s.Color == ConsoleColor.Yellow);
	}

	[Fact]
	public void Highlight_Comment_IsDarkGreenToEnd()
	{
		var spans = Create("run").Highlight("run(\"x\") -- tail");
		Assert.Contains(spans, s => s.Start == 9 && s.Length == 7 && s.Color == ConsoleColor.DarkGreen);
		Assert.DoesNotContain(spans, s => s.Color == ConsoleColor.Yellow && s.Start > 9);
	}

	[Fact]
	public void Highlight_CommentMarkerInsideString_NotComment()
	{
		var spans = Create("echo").Highlight("echo(\"-- x\")");
		Assert.Contains(spans, s => s.Color == ConsoleColor.Yellow);
		Assert.DoesNotContain(spans, s => s.Color == ConsoleColor.DarkGreen);
	}

	[Fact]
	public void Highlight_EmptyInput_NoSpans()
	{
		Assert.Empty(Create("git").Highlight("   "));
	}
}

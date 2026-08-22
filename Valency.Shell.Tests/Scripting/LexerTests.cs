namespace Valency.Shell.Tests.Scripting;

public class LexerTests
{
	private static List<(TokenType Type, string Text)> Tokens(string input) =>
		ShellLexer.Tokenize(input).Select(t => (t.Type, t.Text)).ToList();

	[Fact]
	public void SimpleCommand_ProducesWords()
	{
		var tokens = Tokens("git status");
		Assert.Equal(
			[(TokenType.Word, "git"), (TokenType.Word, "status"), (TokenType.EndOfFile, "")],
			tokens);
	}

	[Fact]
	public void Operators_AreRecognized()
	{
		Assert.Equal([(TokenType.Word, "a"), (TokenType.AndIf, "&&"), (TokenType.Word, "b"), (TokenType.EndOfFile, "")], Tokens("a && b"));
		Assert.Equal([(TokenType.Word, "a"), (TokenType.OrIf, "||"), (TokenType.Word, "b"), (TokenType.EndOfFile, "")], Tokens("a || b"));
		Assert.Equal([(TokenType.Word, "a"), (TokenType.Pipe, "|"), (TokenType.Word, "b"), (TokenType.EndOfFile, "")], Tokens("a | b"));
		Assert.Equal([(TokenType.Word, "a"), (TokenType.Background, "&"), (TokenType.Word, "b"), (TokenType.EndOfFile, "")], Tokens("a & b"));
		Assert.Equal([(TokenType.Bang, "!"), (TokenType.Word, "a"), (TokenType.EndOfFile, "")], Tokens("! a"));
	}

	[Fact]
	public void SingleQuotes_BecomeSingleQuotedPart()
	{
		var tokens = ShellLexer.Tokenize("echo 'a b'");
		var word = tokens[1].Word!;
		Assert.Equal(new SingleQuotedPart("a b"), Assert.Single(word.Parts));
	}

	[Fact]
	public void DoubleQuotes_BecomeQuotedLiteral()
	{
		var tokens = ShellLexer.Tokenize("echo \"a$X\"");
		var word = tokens[1].Word!;
		Assert.Equal(new LiteralPart("a$X", Quoted: true), Assert.Single(word.Parts));
	}

	[Fact]
	public void Redirects_WithFd_AreRecognized()
	{
		Assert.Equal([(TokenType.Word, "cat"), (TokenType.GreatAnd, "2>&"), (TokenType.Word, "1"), (TokenType.EndOfFile, "")], Tokens("cat 2>&1"));
		Assert.Equal([(TokenType.Word, "echo"), (TokenType.RedirectOut, ">"), (TokenType.Word, "f"), (TokenType.EndOfFile, "")], Tokens("echo > f"));
		Assert.Equal([(TokenType.Word, "cmd"), (TokenType.RedirectOut, "2>"), (TokenType.Word, "f"), (TokenType.EndOfFile, "")], Tokens("cmd 2> f"));
		Assert.Equal([(TokenType.Word, "cmd"), (TokenType.RedirectIn, "<"), (TokenType.Word, "f"), (TokenType.EndOfFile, "")], Tokens("cmd < f"));
	}

	[Fact]
	public void CommandSubstitution_IsPartOfWord()
	{
		var tokens = ShellLexer.Tokenize("echo $(ls)");
		var word = tokens[1].Word!;
		Assert.Equal(new CommandSubPart("ls"), Assert.Single(word.Parts));
	}

	[Fact]
	public void ControlKeyword_ScansExpression()
	{
		var tokens = Tokens("if ($x < 3) { }");
		Assert.Equal(
			[(TokenType.Word, "if"), (TokenType.Expression, "$x < 3"), (TokenType.LBrace, "{"), (TokenType.RBrace, "}"), (TokenType.EndOfFile, "")],
			tokens);
	}

	[Fact]
	public void UnclosedSingleQuote_Throws()
	{
		Assert.Throws<SyntaxError>(() => ShellLexer.Tokenize("echo 'abc"));
	}
}

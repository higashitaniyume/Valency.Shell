using Valency.Shell;

namespace Valency.Shell.Tests;

public class CommandParserTests
{
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("git", new[] { "git" })]
    [InlineData("git status", new[] { "git", "status" })]
    [InlineData("  git   status  ", new[] { "git", "status" })]
    [InlineData("echo \"hello world\"", new[] { "echo", "hello world" })]
    [InlineData("echo \"\"", new[] { "echo", "" })]
    [InlineData("\"C:\\Program Files\\app.exe\" run", new[] { "C:\\Program Files\\app.exe", "run" })]
    [InlineData("echo \"a \\\"b\\\" c\"", new[] { "echo", "a \"b\" c" })]
    [InlineData("echo a\"b\"c", new[] { "echo", "abc" })]
    public void Split_ParsesExpectedTokens(string input, string[] expected)
    {
        Assert.Equal(expected, CommandParser.Split(input));
    }

    [Fact]
    public void Split_UnclosedQuote_Throws()
    {
        Assert.Throws<FormatException>(() => CommandParser.Split("echo \"abc"));
    }
}

using Valency.Shell;

namespace Valency.Shell.Tests;

public class CommandParserTokenTests
{
    [Fact]
    public void SingleQuotes_SegmentIsNonExpandable()
    {
        var tokens = CommandParser.SplitTokens("echo '$PATH'");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("$PATH", tokens[1].Text);
        Assert.False(tokens[1].Expandable);
    }

    [Fact]
    public void DoubleQuotes_SegmentIsExpandable()
    {
        var tokens = CommandParser.SplitTokens("echo \"$PATH\"");
        Assert.Equal("$PATH", tokens[1].Text);
        Assert.True(tokens[1].Expandable);
    }

    [Fact]
    public void MixedSegments_PreservedPerSegment()
    {
        var tokens = CommandParser.SplitTokens("echo a'$B'c");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("a$Bc", tokens[1].Text);
        Assert.Equal(
            [new TokenSegment("a", true), new TokenSegment("$B", false), new TokenSegment("c", true)],
            tokens[1].Segments);
    }

    [Fact]
    public void SingleQuotes_Unclosed_Throws()
    {
        Assert.Throws<FormatException>(() => CommandParser.SplitTokens("echo 'abc"));
    }

    [Fact]
    public void EmptySingleQuotedString_ProducesEmptyToken()
    {
        var tokens = CommandParser.SplitTokens("echo ''");
        Assert.Equal(2, tokens.Count);
        Assert.Equal("", tokens[1].Text);
    }
}

using Valency.Shell;

namespace Valency.Shell.Tests;

public class LineParserTests
{
    [Fact]
    public void Parse_SingleCommand_NoConnector()
    {
        var commands = LineParser.Parse("git status");
        Assert.Single(commands);
        Assert.Equal("git status", commands[0].RawText);
        Assert.Equal(Connector.None, commands[0].Connector);
    }

    [Fact]
    public void Parse_Semicolon_TwoCommands()
    {
        var commands = LineParser.Parse("echo a; echo b");
        Assert.Equal(2, commands.Count);
        Assert.Equal("echo a", commands[0].RawText);
        Assert.Equal(Connector.Semicolon, commands[0].Connector);
        Assert.Equal("echo b", commands[1].RawText);
        Assert.Equal(Connector.None, commands[1].Connector);
    }

    [Fact]
    public void Parse_AndOr_AssignsConnectors()
    {
        var commands = LineParser.Parse("a && b || c");
        Assert.Equal(3, commands.Count);
        Assert.Equal(Connector.And, commands[0].Connector);
        Assert.Equal(Connector.Or, commands[1].Connector);
        Assert.Equal(Connector.None, commands[2].Connector);
    }

    [Fact]
    public void Parse_SeparatorsInsideQuotes_AreLiteral()
    {
        var commands = LineParser.Parse("echo \"a;b&&c||d\"");
        Assert.Single(commands);
        Assert.Equal("echo \"a;b&&c||d\"", commands[0].RawText);
    }

    [Fact]
    public void Parse_EmptyCommands_AreSkipped()
    {
        var commands = LineParser.Parse("a;;b");
        Assert.Equal(2, commands.Count);
        Assert.Equal("a", commands[0].RawText);
        Assert.Equal("b", commands[1].RawText);
    }

    [Fact]
    public void Parse_SinglePipe_SetsPipeConnector()
    {
        var commands = LineParser.Parse("a | b");
        Assert.Equal(2, commands.Count);
        Assert.Equal(Connector.Pipe, commands[0].Connector);
        Assert.Equal("b", commands[1].RawText);
    }

    [Fact]
    public void Parse_SingleAmpersand_SetsBackgroundConnector()
    {
        var commands = LineParser.Parse("a & b");
        Assert.Equal(2, commands.Count);
        Assert.Equal(Connector.Background, commands[0].Connector);
        Assert.Equal("b", commands[1].RawText);
    }

    [Fact]
    public void Parse_PipeTakesPrecedenceOverAnd()
    {
        var commands = LineParser.Parse("a | b && c");
        Assert.Equal(3, commands.Count);
        Assert.Equal(Connector.Pipe, commands[0].Connector);
        Assert.Equal(Connector.And, commands[1].Connector);
    }

    [Fact]
    public void Parse_PipeInsideQuotes_IsLiteral()
    {
        var commands = LineParser.Parse("echo \"a|b\"");
        Assert.Single(commands);
        Assert.Equal("echo \"a|b\"", commands[0].RawText);
    }
}

public class ShellRunNextTests
{
    [Fact]
    public void Executed_And_Success_Continues()
    {
        Assert.True(Shell.RunNext(executed: true, Connector.And, 0));
    }

    [Fact]
    public void Executed_And_Failure_ShortCircuits()
    {
        Assert.False(Shell.RunNext(executed: true, Connector.And, 1));
    }

    [Fact]
    public void Executed_Or_Failure_Continues()
    {
        Assert.True(Shell.RunNext(executed: true, Connector.Or, 1));
    }

    [Fact]
    public void Executed_Or_Success_ShortCircuits()
    {
        Assert.False(Shell.RunNext(executed: true, Connector.Or, 0));
    }

    [Fact]
    public void Executed_Semicolon_AlwaysContinues()
    {
        Assert.True(Shell.RunNext(executed: true, Connector.Semicolon, 1));
        Assert.True(Shell.RunNext(executed: true, Connector.None, 1));
    }

    [Fact]
    public void Skipped_And_StaysSkipped()
    {
        Assert.False(Shell.RunNext(executed: false, Connector.And, 0));
    }

    [Fact]
    public void Skipped_Or_Resumes()
    {
        Assert.True(Shell.RunNext(executed: false, Connector.Or, 0));
    }

    [Fact]
    public void Skipped_Semicolon_Resumes()
    {
        Assert.True(Shell.RunNext(executed: false, Connector.Semicolon, 0));
        Assert.True(Shell.RunNext(executed: false, Connector.None, 0));
    }
}

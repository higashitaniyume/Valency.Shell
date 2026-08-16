using Valency.Shell;

namespace Valency.Shell.Tests.Scripting;

public class ParserTests
{
    private static CompoundList Body(string text) => Assert.IsType<CompoundList>(Parser.Parse(text).Body);

    private static Command FirstCommand(string text)
    {
        var entry = Assert.Single(Body(text).Entries);
        return entry.Command.Pipeline.Commands[0];
    }

    [Fact]
    public void SimpleCommand_ParsesWords()
    {
        var cmd = Assert.IsType<SimpleCommand>(FirstCommand("git status"));
        Assert.Equal(["git", "status"], cmd.Words.Select(w => w.Raw));
    }

    [Fact]
    public void Assignment_ParsesAsAssignment()
    {
        var cmd = Assert.IsType<SimpleCommand>(FirstCommand("x=hello"));
        Assert.Empty(cmd.Words);
        var assignment = Assert.Single(cmd.Assignments);
        Assert.Equal("x", assignment.Name);
        Assert.Equal("hello", assignment.Value.Raw);
    }

    [Fact]
    public void AndOr_ParsesConnectors()
    {
        var full = Assert.IsType<AndOr>(Assert.Single(Body("a && b || c").Entries).Command);
        Assert.Equal(2, full.Rest.Count);
        Assert.Equal(Connector.And, full.Rest[0].Op);
        Assert.Equal(Connector.Or, full.Rest[1].Op);
    }

    [Fact]
    public void Pipeline_ParsesStages()
    {
        var pipeline = Assert.IsType<AndOr>(Assert.Single(Body("a | b | c").Entries).Command).Pipeline;
        Assert.Equal(3, pipeline.Commands.Count);
    }

    [Fact]
    public void Negate_Parses()
    {
        var pipeline = Assert.IsType<AndOr>(Assert.Single(Body("! a").Entries).Command).Pipeline;
        Assert.True(pipeline.Negate);
    }

    [Fact]
    public void Background_ParsesAsConnector()
    {
        var entry = Assert.Single(Body("a &").Entries);
        Assert.Equal(Connector.Background, entry.Connector);
    }

    [Fact]
    public void If_Parses()
    {
        var ifCommand = Assert.IsType<IfCommand>(FirstCommand("if true; then echo y; else echo n; fi"));
        Assert.Single(ifCommand.Branches);
        Assert.NotNull(ifCommand.Else);
    }

    [Fact]
    public void While_Parses()
    {
        var whileCommand = Assert.IsType<WhileCommand>(FirstCommand("while true; do echo hi; done"));
        Assert.False(whileCommand.Until);
    }

    [Fact]
    public void For_Parses()
    {
        var forCommand = Assert.IsType<ForInCommand>(FirstCommand("for i in a b; do echo $i; done"));
        Assert.Equal("i", forCommand.Variable);
        Assert.Equal(2, forCommand.Items!.Count);
    }

    [Fact]
    public void Function_Parses()
    {
        var function = Assert.IsType<FunctionDef>(FirstCommand("f() { echo hi; }"));
        Assert.Equal("f", function.Name);
    }

    [Fact]
    public void Case_Parses()
    {
        var caseCommand = Assert.IsType<CaseCommand>(FirstCommand("case $x in a) echo 1;; *) echo 2;; esac"));
        Assert.Equal(2, caseCommand.Arms.Count);
    }

    [Fact]
    public void Subshell_Parses()
    {
        var subshell = Assert.IsType<Subshell>(FirstCommand("( echo hi )"));
        Assert.Single(subshell.Body.Entries);
    }

    [Fact]
    public void Semicolon_SeparatesCommands()
    {
        var entries = Body("echo a; echo b").Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(Connector.Semicolon, entries[0].Connector);
    }

    [Fact]
    public void IncompleteCase_IsDetected()
    {
        Assert.True(Parser.IsIncomplete("case x in"));
        Assert.False(Parser.IsIncomplete("case x in a) echo 1;; esac"));
    }
}

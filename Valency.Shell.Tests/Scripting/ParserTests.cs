using Valency.Shell;

namespace Valency.Shell.Tests.Scripting;

public class ParserTests
{
    private static IReadOnlyList<Statement> Statements(string text) => Parser.Parse(text).Statements;

    [Fact]
    public void SimpleCommand_ParsesWords()
    {
        var statement = Assert.IsType<CommandStatement>(Assert.Single(Statements("git status")));
        var command = Assert.IsType<SimpleCommand>(statement.Command.Pipeline.Commands[0]);
        Assert.Equal(["git", "status"], command.Words.Select(w => w.Raw));
    }

    [Fact]
    public void Assignment_ParsesAsExpression()
    {
        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(Statements("$x = 5")));
        Assert.Equal("$x = 5", statement.Expression);
    }

    [Fact]
    public void Assignment_InlineForm()
    {
        var statement = Assert.IsType<ExpressionStatement>(Assert.Single(Statements("$x=5")));
        Assert.Equal("$x=5", statement.Expression);
    }

    [Fact]
    public void AndOr_ParsesConnectors()
    {
        var statement = Assert.IsType<CommandStatement>(Assert.Single(Statements("a && b || c")));
        Assert.Equal(2, statement.Command.Rest.Count);
        Assert.Equal(Connector.And, statement.Command.Rest[0].Op);
        Assert.Equal(Connector.Or, statement.Command.Rest[1].Op);
    }

    [Fact]
    public void Pipeline_ParsesStages()
    {
        var statement = Assert.IsType<CommandStatement>(Assert.Single(Statements("a | b | c")));
        Assert.Equal(3, statement.Command.Pipeline.Commands.Count);
    }

    [Fact]
    public void Negate_Parses()
    {
        var statement = Assert.IsType<CommandStatement>(Assert.Single(Statements("! a")));
        Assert.True(statement.Command.Pipeline.Negate);
    }

    [Fact]
    public void Background_Parses()
    {
        var statement = Assert.IsType<CommandStatement>(Assert.Single(Statements("a &")));
        Assert.True(statement.Background);
    }

    [Fact]
    public void If_Parses()
    {
        var statement = Assert.IsType<IfStatement>(Assert.Single(Statements("if ($x) { echo y } else { echo n }")));
        Assert.Equal("$x", statement.Condition);
        Assert.NotNull(statement.Else);
    }

    [Fact]
    public void If_ElseIf_Parses()
    {
        var statement = Assert.IsType<IfStatement>(Assert.Single(Statements("if ($a) { } else if ($b) { } else { }")));
        Assert.Single(statement.ElseIfs);
        Assert.NotNull(statement.Else);
    }

    [Fact]
    public void While_Parses()
    {
        var statement = Assert.IsType<WhileStatement>(Assert.Single(Statements("while ($x) { }")));
        Assert.False(statement.Until);
        Assert.Equal("$x", statement.Condition);
    }

    [Fact]
    public void For_Parses()
    {
        var statement = Assert.IsType<ForStatement>(Assert.Single(Statements("for ($i = 0; $i < 3; $i++) { }")));
        Assert.Equal("$i = 0", statement.Init);
        Assert.Equal("$i < 3", statement.Condition);
        Assert.Equal("$i++", statement.Post);
    }

    [Fact]
    public void Function_Parses()
    {
        var statement = Assert.IsType<FunctionDecl>(Assert.Single(Statements("function greet($name) { }")));
        Assert.Equal("greet", statement.Name);
        Assert.Equal(["name"], statement.Parameters);
    }

    [Fact]
    public void Return_Parses()
    {
        var statement = Assert.IsType<ReturnStatement>(Assert.Single(Statements("return 5")));
        Assert.Equal("5", statement.Value);
    }

    [Fact]
    public void Semicolon_SeparatesStatements()
    {
        Assert.Equal(2, Statements("echo a; echo b").Count);
    }

    [Fact]
    public void Block_ParsesNestedStatements()
    {
        var block = Assert.IsType<BlockStatement>(Assert.Single(Statements("{ echo a; echo b }")));
        Assert.Equal(2, block.Statements.Count);
    }
}

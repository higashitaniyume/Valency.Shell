using Valency.Shell;

namespace Valency.Shell.Tests;

public class VariableExpanderTests
{
    private sealed class FakeSource(Dictionary<string, string> vars) : IVariableSource
    {
        public bool TryGet(string name, out string? value)
        {
            return vars.TryGetValue(name, out value);
        }
    }

    private static VariableExpander Create(params (string Name, string Value)[] vars)
    {
        return new VariableExpander(new FakeSource(vars.ToDictionary(v => v.Name, v => v.Value)));
    }

    private static Token Tok(string text, bool expand = true)
    {
        return new Token([new TokenSegment(text, expand)]);
    }

    [Fact]
    public void Expand_SimpleVariable()
    {
        Assert.Equal("C:\\bin", Create(("PATH", "C:\\bin")).Expand(Tok("$PATH")));
    }

    [Fact]
    public void Expand_BracedVariable_AllowsAdjacentChars()
    {
        Assert.Equal("C:\\binX", Create(("PATH", "C:\\bin")).Expand(Tok("${PATH}X")));
    }

    [Fact]
    public void Expand_EnvPrefix_CaseInsensitive()
    {
        var expander = Create(("PATH", "C:\\bin"));
        Assert.Equal("C:\\bin", expander.Expand(Tok("$env:PATH")));
        Assert.Equal("C:\\bin", expander.Expand(Tok("$ENV:PATH")));
    }

    [Fact]
    public void Expand_QuestionMark()
    {
        Assert.Equal("42", Create(("?", "42")).Expand(Tok("$?")));
    }

    [Fact]
    public void Expand_Undefined_BecomesEmpty()
    {
        Assert.Equal("ab", Create().Expand(Tok("a${NOPE}b")));
    }

    [Fact]
    public void Expand_EscapedDollar_StaysLiteral()
    {
        Assert.Equal("$PATH", Create(("PATH", "C:\\bin")).Expand(Tok("\\$PATH")));
    }

    [Fact]
    public void Expand_NonExpandableSegment_StaysLiteral()
    {
        Assert.Equal("$PATH", Create(("PATH", "C:\\bin")).Expand(Tok("$PATH", expand: false)));
    }

    [Fact]
    public void Expand_LoneDollar_StaysLiteral()
    {
        Assert.Equal("a$", Create().Expand(Tok("a$")));
        Assert.Equal("a$-b", Create().Expand(Tok("a$-b")));
    }

    [Fact]
    public void ExpandTilde_AtStart_ExpandsToUserProfile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(home, VariableExpander.ExpandTilde("~", expandable: true));
        Assert.Equal(home + "\\docs", VariableExpander.ExpandTilde("~\\docs", expandable: true));
        Assert.Equal(home + "/docs", VariableExpander.ExpandTilde("~/docs", expandable: true));
    }

    [Fact]
    public void ExpandTilde_NotExpanded_WhenNotAtStartOrQuoted()
    {
        Assert.Equal("a~", VariableExpander.ExpandTilde("a~", expandable: true));
        Assert.Equal("~user", VariableExpander.ExpandTilde("~user", expandable: true));
        Assert.Equal("~", VariableExpander.ExpandTilde("~", expandable: false));
    }
}

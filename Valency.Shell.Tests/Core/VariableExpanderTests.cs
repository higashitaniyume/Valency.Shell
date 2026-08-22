namespace Valency.Shell.Tests.Core;

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

	[Fact]
	public void Expand_SimpleVariable()
	{
		Assert.Equal("C:\\bin", Create(("PATH", "C:\\bin")).ExpandText("$PATH"));
	}

	[Fact]
	public void Expand_BracedVariable_AllowsAdjacentChars()
	{
		Assert.Equal("C:\\binX", Create(("PATH", "C:\\bin")).ExpandText("${PATH}X"));
	}

	[Fact]
	public void Expand_EnvPrefix_CaseInsensitive()
	{
		var expander = Create(("PATH", "C:\\bin"));
		Assert.Equal("C:\\bin", expander.ExpandText("$env:PATH"));
		Assert.Equal("C:\\bin", expander.ExpandText("$ENV:PATH"));
	}

	[Fact]
	public void Expand_QuestionMark()
	{
		Assert.Equal("42", Create(("?", "42")).ExpandText("$?"));
	}

	[Fact]
	public void Expand_PositionalAndSpecial()
	{
		var expander = Create(("1", "first"), ("@", "a b c"), ("#", "3"));
		Assert.Equal("first", expander.ExpandText("$1"));
		Assert.Equal("a b c", expander.ExpandText("$@"));
		Assert.Equal("3", expander.ExpandText("$#"));
	}

	[Fact]
	public void Expand_Undefined_BecomesEmpty()
	{
		Assert.Equal("ab", Create().ExpandText("a${NOPE}b"));
	}

	[Fact]
	public void Expand_EscapedDollar_StaysLiteral()
	{
		Assert.Equal("$PATH", Create(("PATH", "C:\\bin")).ExpandText("\\$PATH"));
	}

	[Fact]
	public void Expand_LoneDollar_StaysLiteral()
	{
		Assert.Equal("a$", Create().ExpandText("a$"));
		Assert.Equal("a$-b", Create().ExpandText("a$-b"));
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

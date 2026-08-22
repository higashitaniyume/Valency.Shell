using Valency.Shell.Prompting;

namespace Valency.Shell.Tests.Host;

public class PromptFormatterTests
{
	[Fact]
	public void AbbreviateHome_HomeDir_IsTilde()
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		Assert.Equal("~", PromptVariableSource.AbbreviateHome(home));
		Assert.Equal("~" + Path.DirectorySeparatorChar + "sub",
			PromptVariableSource.AbbreviateHome(Path.Combine(home, "sub")));
		Assert.Equal(Path.DirectorySeparatorChar + "etc",
			PromptVariableSource.AbbreviateHome(Path.DirectorySeparatorChar + "etc"));
	}

	[Fact]
	public void BuildKali_Admin_UsesAtAndSharp()
	{
		var formatter = new PromptFormatter(() => true);
		var prompt = formatter.BuildKali();
		var stripped = PromptFormatter.StripAnsi(prompt.Raw);
		Assert.Contains("┌──(", stripped);
		Assert.Contains("└─#", PromptFormatter.StripAnsi(prompt.LastLine));
		Assert.Contains("@", stripped);
		Assert.Equal(4, prompt.CursorOffset); // "└─# "
	}

	[Fact]
	public void BuildKali_NonAdmin_UsesAtConnectorAndDollarSharp()
	{
		var formatter = new PromptFormatter(() => false);
		var prompt = formatter.BuildKali();
		var stripped = PromptFormatter.StripAnsi(prompt.Raw);
		Assert.Contains("└─$", stripped);
		Assert.Contains("@" + Environment.MachineName, stripped);
	}

	[Fact]
	public void BuildPlain_AdminFormat()
	{
		var formatter = new PromptFormatter(() => true);
		var prompt = formatter.BuildPlain();
		var stripped = PromptFormatter.StripAnsi(prompt.Raw);
		Assert.StartsWith(Environment.UserName, stripped);
		Assert.Contains("@", stripped);
		Assert.EndsWith("# ", stripped);
	}

	[Fact]
	public void BuildCustom_ExpandsVariables()
	{
		var formatter = new PromptFormatter(() => true);
		var prompt = formatter.BuildCustom("$USER@$PWD> ");
		var stripped = PromptFormatter.StripAnsi(prompt.Raw);
		Assert.StartsWith(Environment.UserName + "@", stripped);
		Assert.EndsWith("> ", stripped);
	}

	[Fact]
	public void VisibleLength_IgnoresAnsi()
	{
		Assert.Equal(3, PromptFormatter.VisibleLength("\x1b[32mabc\x1b[0m"));
	}
}

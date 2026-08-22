using Valency.Shell.Builtins;

namespace Valency.Shell.Tests.Builtins;

public class ArgParserTests
{
	private static readonly CommandSpec Spec = new()
	{
		Name = "test",
		Summary = "test",
		Options =
		[
			new("ignore-case", 'i', "ignore", true),
			new("tail", 'n', "tail", false, "N"),
			new("level", null, "level", false, "LEVEL"),
		],
	};

	private static ParseResult Parse(params string[] args)
	{
		var result = ArgParser.Parse(args, Spec, out var error);
		Assert.NotNull(result);
		Assert.Null(error);
		return result!;
	}

	[Fact]
	public void Parse_LongOptionWithValue()
	{
		var r = Parse("--tail", "10");
		Assert.Equal(10, r.GetInt("tail"));
	}

	[Fact]
	public void Parse_ShortOptionWithValue()
	{
		var r = Parse("-n", "5");
		Assert.Equal(5, r.GetInt("tail"));
	}

	[Fact]
	public void Parse_EqualsAndColonInlineValues()
	{
		Assert.Equal(3, Parse("--tail=3").GetInt("tail"));
		Assert.Equal(4, Parse("--tail:4").GetInt("tail"));
		Assert.Equal(5, Parse("-n:5").GetInt("tail"));
	}

	[Fact]
	public void Parse_FlagOptions()
	{
		Assert.True(Parse("-i").Has("ignore-case"));
		Assert.True(Parse("--ignore-case").Has("ignore-case"));
		Assert.False(Parse().Has("ignore-case"));
	}

	[Fact]
	public void Parse_Positionals()
	{
		var r = Parse("pattern", "file1", "file2");
		Assert.Equal(["pattern", "file1", "file2"], r.Positionals);
	}

	[Fact]
	public void Parse_HelpRequested()
	{
		Assert.True(Parse("-h").HelpRequested);
		Assert.True(Parse("--help").HelpRequested);
	}

	[Fact]
	public void Parse_DoubleDash_EverythingAfterIsPositional()
	{
		var r = Parse("--", "--tail");
		Assert.Equal(["--tail"], r.Positionals);
	}

	[Fact]
	public void Parse_UnknownOption_ReturnsError()
	{
		var result = ArgParser.Parse(["--nope"], Spec, out var error);
		Assert.Null(result);
		Assert.NotNull(error);
	}

	[Fact]
	public void Parse_ValueOptionMissingValue_ReturnsError()
	{
		var result = ArgParser.Parse(["--tail"], Spec, out var error);
		Assert.Null(result);
		Assert.NotNull(error);
	}
}

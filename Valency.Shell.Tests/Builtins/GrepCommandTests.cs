using Valency.Shell.Builtins;

namespace Valency.Shell.Tests.Builtins;

public class GrepCommandTests
{
	private static readonly string NL = Environment.NewLine;

	private sealed class FakeContext : IShellContext
	{
		public int LastExitCode { get; set; }
		public string? PreviousDirectory { get; set; }
		public string CurrentDirectory { get; set; } = Environment.CurrentDirectory;
		public bool ExitRequested => false;
		public int RequestedExitCode => 0;
		public TextReader? PipelineInput { get; init; }
		public void RequestExit(int exitCode) { }
		public void PrintJobs() { }
		public string? GetVariable(string name) => null;
		public void SetVariable(string name, string value, bool exported) { }
		public void ExportVariable(string name) { }
		public void UnsetVariable(string name) { }
		public void ShiftArguments(int count) { }
		public int RunScriptFile(string path) => 0;
	}

	private static StringWriter Run(string[] args, string input)
	{
		var parse = ArgParser.Parse(args, new GrepCommand().Spec, out _)!;
		var context = new FakeContext { PipelineInput = new StringReader(input) };
		var output = new StringWriter();
		var original = Console.Out;
		try
		{
			Console.SetOut(output);
			new GrepCommand().Execute(parse, context);
		}
		finally
		{
			Console.SetOut(original);
		}
		return output;
	}

	[Fact]
	public void Grep_BasicMatch()
	{
		var output = Run(["foo"], "foo\nbar\nfoobar\nbaz\n");
		Assert.Equal($"foo{NL}foobar{NL}", output.ToString());
	}

	[Fact]
	public void Grep_IgnoreCase()
	{
		var output = Run(["-i", "FOO"], "foo\nbar\n");
		Assert.Equal($"foo{NL}", output.ToString());
	}

	[Fact]
	public void Grep_InvertMatch()
	{
		var output = Run(["-v", "foo"], "foo\nbar\n");
		Assert.Equal($"bar{NL}", output.ToString());
	}

	[Fact]
	public void Grep_LineNumber()
	{
		var output = Run(["-n", "bar"], "foo\nbar\nbar\n");
		Assert.Equal($"2:bar{NL}3:bar{NL}", output.ToString());
	}

	[Fact]
	public void Grep_Count()
	{
		var output = Run(["-c", "bar"], "foo\nbar\nbar\n");
		Assert.Equal($"2{NL}", output.ToString());
	}

	[Fact]
	public void Grep_NoMatch_ReturnsNonZero()
	{
		var parse = ArgParser.Parse(["nomatch"], new GrepCommand().Spec, out _)!;
		var context = new FakeContext { PipelineInput = new StringReader("foo\nbar\n") };
		var code = new GrepCommand().Execute(parse, context);
		Assert.Equal(1, code);
	}
}

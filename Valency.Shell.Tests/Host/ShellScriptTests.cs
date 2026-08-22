using Serilog;
using Valency.Shell.Builtins;
using Valency.Shell.Prompting;

namespace Valency.Shell.Tests.Host;

public class ShellScriptTests
{
	private static Shell CreateShell()
	{
		var logger = new LoggerConfiguration().CreateLogger();
		var promptSettings = new PromptSettings();
		var promptFormatter = new PromptFormatter();
		var builtins = BuiltinCommands.CreateDefault("unused.log", 0, promptSettings);
		return new Shell(builtins, logger, promptFormatter, promptSettings);
	}

	private static string Capture(Action action)
	{
		var writer = new StringWriter();
		var previous = Console.Out;
		try
		{
			Console.SetOut(writer);
			action();
		}
		finally
		{
			Console.SetOut(previous);
		}
		return writer.ToString();
	}

	[Fact]
	public void Exit_ReturnsCode()
	{
		var shell = CreateShell();
		var code = shell.ExecuteLine("exit(7)");
		Assert.Equal(7, code);
		Assert.True(shell.ExitRequested);
	}

	[Fact]
	public void Echo_PrintsText()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("echo(\"hi\")"));
		Assert.Equal("hi" + Environment.NewLine, output);
	}

	[Fact]
	public void Expression_EchoesResult()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("1 + 2 * 3"));
		Assert.Equal("7" + Environment.NewLine, output);
	}

	[Fact]
	public void Capture_BuiltinOutput()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("out = capture(\"echo\", \"nested\") echo(out)"));
		Assert.Equal("nested" + Environment.NewLine, output);
	}

	[Fact]
	public void If_RunsBranch()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("if true then echo(\"y\") else echo(\"n\") end"));
		Assert.Equal("y" + Environment.NewLine, output);
	}

	[Fact]
	public void If_ElseIf_RunsBranch()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("x = 2 if x == 1 then echo(\"one\") elseif x == 2 then echo(\"two\") else echo(\"other\") end"));
		Assert.Equal("two" + Environment.NewLine, output);
	}

	[Fact]
	public void Function_And_Parameters()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("function greet(name) echo(\"hello\", name) end greet(\"world\")"));
		Assert.Equal("hello world" + Environment.NewLine, output);
	}

	[Fact]
	public void ForLoop_Iterates()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("for i = 0, 1 do echo(i) end"));
		Assert.Equal("0" + Environment.NewLine + "1" + Environment.NewLine, output);
	}

	[Fact]
	public void WhileLoop_WithBreak()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("i = 0 while true do i = i + 1 if i >= 3 then break end end echo(i)"));
		Assert.Equal("3" + Environment.NewLine, output);
	}

	[Fact]
	public void Status_ReflectsLastCommandCode()
	{
		var shell = CreateShell();
		Assert.Equal(1, shell.ExecuteLine("run(\"false\") code = status() exit(code)"));
	}

	[Fact]
	public void Args_ExposePositionals()
	{
		var shell = CreateShell();
		shell.PositionalArgs = ["a", "b"];
		var output = Capture(() => shell.ExecuteLine("echo(args[1], args[2], #args)"));
		Assert.Equal("a b 2" + Environment.NewLine, output);
	}

	[Fact]
	public void Pipe_EndingInBuiltin_PrintsToConsole()
	{
		var shell = CreateShell();
		var output = Capture(() => shell.ExecuteLine("pipe({\"cmd\", \"/c\", \"echo\", \"hi\"}, {\"grep\", \"hi\"})"));
		Assert.Equal("hi" + Environment.NewLine, output);
	}

	[Fact]
	public void Redirect_ExternalCommand_ToFile()
	{
		var shell = CreateShell();
		var dir = Path.Combine(Path.GetTempPath(), "valency-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var output = Path.Combine(dir, "out.txt");
			// Lua 字符串里反斜杠是转义符，用正斜杠路径
			var luaPath = output.Replace('\\', '/');
			shell.ExecuteLine($"run(\"cmd\", \"/c\", \"echo\", \"hello\", {{ out = \"{luaPath}\" }})");
			Assert.Contains("hello", File.ReadAllText(output));
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	[Fact]
	public void RunScript_ExecutesWholeFile()
	{
		var shell = CreateShell();
		var dir = Path.Combine(Path.GetTempPath(), "valency-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var script = Path.Combine(dir, "s.lua");
			File.WriteAllText(script, "if true then echo(\"yes\") end\nfor i = 0, 1 do echo(i) end\n");
			using var reader = new StreamReader(script);
			var output = Capture(() => shell.RunScript(reader));
			Assert.Contains("yes", output);
			Assert.Contains("0", output);
			Assert.Contains("1", output);
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}
}

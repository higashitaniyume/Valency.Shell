using Serilog;
using Valency.Shell;
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
        var code = shell.ExecuteLine("exit 7");
        Assert.Equal(7, code);
        Assert.True(shell.ExitRequested);
    }

    [Fact]
    public void AndOr_ShortCircuits()
    {
        var shell = CreateShell();
        Assert.Equal(1, shell.ExecuteLine("true && false"));
        Assert.Equal(0, shell.ExecuteLine("false || true"));
    }

    [Fact]
    public void Echo_PrintsText()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("echo hi"));
        Assert.Equal("hi" + Environment.NewLine, output);
    }

    [Fact]
    public void Expression_IsEvaluated()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("$x = 1 + 2 * 3; echo $x"));
        Assert.Equal("7" + Environment.NewLine, output);
    }

    [Fact]
    public void CommandSubstitution_CapturesOutput()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("echo $(echo nested)"));
        Assert.Equal("nested" + Environment.NewLine, output);
    }

    [Fact]
    public void If_RunsBranch()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("if (true) { echo y } else { echo n }"));
        Assert.Equal("y" + Environment.NewLine, output);
    }

    [Fact]
    public void If_ElseIf_RunsBranch()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("$x = 2; if ($x == 1) { echo one } else if ($x == 2) { echo two } else { echo other }"));
        Assert.Equal("two" + Environment.NewLine, output);
    }

    [Fact]
    public void Function_And_Parameters()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("function greet($name) { echo hello $name }; greet world"));
        Assert.Equal("hello world" + Environment.NewLine, output);
    }

    [Fact]
    public void ForLoop_Iterates()
    {
        var shell = CreateShell();
        var output = Capture(() => shell.ExecuteLine("for ($i = 0; $i < 2; $i++) { echo $i }"));
        Assert.Equal("0" + Environment.NewLine + "1" + Environment.NewLine, output);
    }

    [Fact]
    public void WhileLoop_WithBreak()
    {
        var shell = CreateShell();
        var output = Capture(() =>
            shell.ExecuteLine("$i = 0; while (true) { $i = $i + 1; if ($i >= 3) { break } }; echo $i"));
        Assert.Equal("3" + Environment.NewLine, output);
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
            shell.ExecuteLine($"cmd /c echo hello > {output}");
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
            var script = Path.Combine(dir, "s.sh");
            File.WriteAllText(script, "if (true) { echo yes }\nfor ($i = 0; $i < 2; $i++) { echo $i }\n");
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

using Valency.Shell.Scripting.Lua;

namespace Valency.Shell.Tests.Scripting;

/// <summary>In-memory ILuaHost that records every call and returns scripted results.</summary>
internal sealed class FakeLuaHost : ILuaHost
{
    public List<string> RunCalls { get; } = [];
    public List<LuaRedirect?> RunRedirects { get; } = [];
    public List<string> CaptureCalls { get; } = [];
    public List<string[]> PipelineStages { get; } = [];
    public List<string> SpawnCalls { get; } = [];
    public List<string> ProbedCommands { get; } = [];
    public List<string> PrintedJobs { get; } = [];

    public int NextRunCode { get; set; }
    public string NextCaptureOutput { get; set; } = "captured";
    public int NextCaptureCode { get; set; }
    public int NextPipelineCode { get; set; }
    public int? NextSpawnId { get; set; } = 7;
    public ISet<string> AvailableCommands { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "echo", "git", "cmd",
    };

    public int LastExitCode { get; set; }
    public int RequestedExitCodeValue { get; private set; }
    public bool ExitRequestedValue { get; private set; }
    public string WorkingDirectory { get; set; } = @"C:\work";

    public int Run(IReadOnlyList<string> argv, IReadOnlyList<LuaRedirect>? redirects)
    {
        RunCalls.Add(string.Join(" ", argv));
        RunRedirects.Add(redirects is null || redirects.Count == 0 ? null : redirects[0]);
        return NextRunCode;
    }

    public CaptureResult Capture(IReadOnlyList<string> argv)
    {
        CaptureCalls.Add(string.Join(" ", argv));
        return new CaptureResult(NextCaptureOutput, NextCaptureCode);
    }

    public int Pipeline(IReadOnlyList<string[]> stages, IReadOnlyList<LuaRedirect>? redirects)
    {
        PipelineStages.AddRange(stages);
        return NextPipelineCode;
    }

    public CaptureResult CapturePipeline(IReadOnlyList<string[]> stages)
    {
        PipelineStages.AddRange(stages);
        return new CaptureResult(NextCaptureOutput, NextCaptureCode);
    }

    public int? Spawn(IReadOnlyList<string> argv)
    {
        SpawnCalls.Add(string.Join(" ", argv));
        return NextSpawnId;
    }

    public void PrintJobs() => PrintedJobs.Add("jobs");

    public bool IsCommandAvailable(string name)
    {
        ProbedCommands.Add(name);
        return AvailableCommands.Contains(name);
    }

    public void RequestExit(int code)
    {
        RequestedExitCodeValue = code;
        ExitRequestedValue = true;
    }

    public bool ExitRequested => ExitRequestedValue;
    public int RequestedExitCode => RequestedExitCodeValue;
    string ILuaHost.CurrentDirectory { get => WorkingDirectory; set => WorkingDirectory = value; }
}

public class LuaApiTests
{
    private static string CaptureOutput(Func<int> action)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }
        return writer.ToString();
    }

    private static string CaptureError(Action action)
    {
        var original = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }
        return writer.ToString();
    }

    [Fact]
    public void Run_MarshalsArgs_AndReturnsCode()
    {
        var host = new FakeLuaHost { NextRunCode = 3 };
        var shell = new LuaShell(host);

        shell.Execute("code = run(\"git\", \"status\", \"-s\")");

        Assert.Equal(3.0, shell.GetGlobal("code")!.Number);
        Assert.Equal("git status -s", host.RunCalls.Single());
    }

    [Fact]
    public void Run_FlattensArrayTables_AndNumbers()
    {
        var host = new FakeLuaHost();
        var shell = new LuaShell(host);

        shell.Execute("run(\"echo\", {\"a\", \"b\"}, 3, true)");

        Assert.Equal("echo a b 3 true", host.RunCalls.Single());
    }

    [Fact]
    public void Run_OptionsTable_MapsToRedirects()
    {
        var host = new FakeLuaHost();
        var shell = new LuaShell(host);

        shell.Execute("run(\"cmd\", { out = \"o.txt\", append = true, input = \"i.txt\" })");

        Assert.Equal(new LuaRedirect(1, LuaRedirectMode.Append, "o.txt"), host.RunRedirects.Single());
    }

    [Fact]
    public void Run_MergeWithoutOut_FailsWithRuntimeError()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var error = CaptureError(() => Assert.Equal(1, shell.Execute("run(\"cmd\", { merge = true })")));
        Assert.Contains("merge", error);
    }

    [Fact]
    public void CommandProxy_InvokesThroughHost()
    {
        var host = new FakeLuaHost { NextRunCode = 0 };
        var shell = new LuaShell(host);

        var code = shell.Execute("git(\"status\")");

        Assert.Equal(0, code);
        Assert.Equal("git status", host.RunCalls.Single());
    }

    [Fact]
    public void CommandProxy_UnknownName_IsNil()
    {
        var host = new FakeLuaHost();
        var shell = new LuaShell(host);

        var output = CaptureOutput(() => shell.Execute("return no_such_command_xyz"));

        Assert.Equal(string.Empty, output); // nil 不回显
        Assert.Null(shell.GetGlobal("no_such_command_xyz"));
        Assert.Contains("no_such_command_xyz", host.ProbedCommands);
    }

    [Fact]
    public void Capture_EchoesTupleOfOutputAndCode()
    {
        var host = new FakeLuaHost { NextCaptureOutput = "out\n", NextCaptureCode = 5 };
        var shell = new LuaShell(host);

        var output = CaptureOutput(() => shell.Execute("capture(\"git\")"));

        Assert.Equal($"out\t5{Environment.NewLine}", output);
        Assert.Equal("git", host.CaptureCalls.Single());
    }

    [Fact]
    public void Pipe_AcceptsStringAndTableStages()
    {
        var host = new FakeLuaHost();
        var shell = new LuaShell(host);

        shell.Execute("pipe(\"cat a.txt\", {\"grep\", \"error\"}, { out = \"o.txt\" })");

        Assert.Equal(2, host.PipelineStages.Count);
        Assert.Equal(["cat", "a.txt"], host.PipelineStages[0]);
        Assert.Equal(["grep", "error"], host.PipelineStages[1]);
    }

    [Fact]
    public void Spawn_ReturnsJobId()
    {
        var host = new FakeLuaHost { NextSpawnId = 9 };
        var shell = new LuaShell(host);

        var output = CaptureOutput(() => shell.Execute("jobid = spawn(\"make\")"));

        Assert.Equal(string.Empty, output); // 作业提示由宿主打印，退出码/id 不回显
        Assert.Equal(9.0, shell.GetGlobal("jobid")!.Number);
        Assert.Equal("make", host.SpawnCalls.Single());
    }

    [Fact]
    public void Jobs_DelegatesToHost()
    {
        var host = new FakeLuaHost();
        var shell = new LuaShell(host);

        shell.Execute("jobs()");

        Assert.Equal("jobs", host.PrintedJobs.Single());
    }

    [Fact]
    public void Exit_StopsChunk_AndReturnsRequestedCode()
    {
        var host = new FakeLuaHost();
        var shell = new LuaShell(host);

        var code = shell.Execute("exit(7) run(\"echo\")");

        Assert.Equal(7, code);
        Assert.True(host.ExitRequestedValue);
        Assert.Equal(7, host.RequestedExitCodeValue);
        Assert.Empty(host.RunCalls); // exit 后的语句不再执行
    }

    [Fact]
    public void Env_ProxiesProcessEnvironment()
    {
        var shell = new LuaShell(new FakeLuaHost());
        Environment.SetEnvironmentVariable("VALENCY_LUA_TEST", "old");

        shell.Execute("env.VALENCY_LUA_TEST = \"new\"");

        Assert.Equal("new", Environment.GetEnvironmentVariable("VALENCY_LUA_TEST"));
        Environment.SetEnvironmentVariable("VALENCY_LUA_TEST", null);
    }

    [Fact]
    public void ArgsTable_ExposesScriptNameAndPositionals()
    {
        var shell = new LuaShell(new FakeLuaHost());
        shell.SetScriptArgs("demo.lua", ["a", "b"]);

        shell.Execute("name = args[0] first = args[1] second = args[2] count = #args");

        Assert.Equal("demo.lua", shell.GetGlobal("name")!.String);
        Assert.Equal("a", shell.GetGlobal("first")!.String);
        Assert.Equal("b", shell.GetGlobal("second")!.String);
        Assert.Equal(2.0, shell.GetGlobal("count")!.Number);
    }

    [Fact]
    public void Status_ReturnsLastExitCode()
    {
        var host = new FakeLuaHost { LastExitCode = 42 };
        var shell = new LuaShell(host);

        var output = CaptureOutput(() => shell.Execute("status()"));

        Assert.Equal($"42{Environment.NewLine}", output);
    }

    [Fact]
    public void SyntaxError_ReturnsTwo_AndPrintsDecoratedMessage()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var error = CaptureError(() => Assert.Equal(2, shell.Execute("if then")));
        Assert.Contains("stdin", error);
    }

    [Fact]
    public void RuntimeError_ReturnsOne_AndPrintsDecoratedMessage()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var error = CaptureError(() => Assert.Equal(1, shell.Execute("error('boom')")));
        Assert.Contains("boom", error);
    }

    [Fact]
    public void BadArgumentType_FailsAsRuntimeError()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var error = CaptureError(() => Assert.Equal(1, shell.Execute("run(function() end)")));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Echo_PrintsExpressionResult()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var output = CaptureOutput(() => shell.Execute("1 + 2"));
        Assert.Equal($"3{Environment.NewLine}", output);
    }

    [Fact]
    public void Echo_SilentForAssignment()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var output = CaptureOutput(() => shell.Execute("x = 5"));
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void FullLuaSemantics_WorkThroughShell()
    {
        var shell = new LuaShell(new FakeLuaHost());
        shell.Execute("""
            local t = setmetatable({ n = 2 }, { __add = function(a, b) return a.n + b.n end })
            local co = coroutine.wrap(function() coroutine.yield(1) return 2 end)
            sum = t + { n = 40 }
            first = co()
            """);

        var output = CaptureOutput(() => shell.Execute("sum, first"));

        Assert.Equal($"42\t1{Environment.NewLine}", output);
    }

    [Fact]
    public void ExecuteFile_RunsChunkWithoutEcho()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var output = CaptureOutput(() => shell.ExecuteFile("demo.lua", "y = 10"));
        Assert.Equal(string.Empty, output);
        Assert.Equal(10.0, shell.GetGlobal("y")!.Number);
    }

    // ---- require / 纯 Lua 库加载 ----

    private sealed class TempModuleDir : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "valency-lua-" + Guid.NewGuid().ToString("N"));
        private readonly string _original = Environment.CurrentDirectory;

        public TempModuleDir()
        {
            Directory.CreateDirectory(Dir);
            Environment.CurrentDirectory = Dir;
        }

        public void Dispose()
        {
            Environment.CurrentDirectory = _original;
            Directory.Delete(Dir, recursive: true);
        }
    }

    [Fact]
    public void Require_LoadsModuleFromCurrentDirectory()
    {
        using var dir = new TempModuleDir();
        File.WriteAllText(Path.Combine(dir.Dir, "greet.lua"), "return { hello = function() return 'hi' end }");
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("m = require('greet') r = m.hello()");

        Assert.Equal("hi", shell.GetGlobal("r")!.String);
    }

    [Fact]
    public void Require_DottedName_ResolvesSubdirectory()
    {
        using var dir = new TempModuleDir();
        Directory.CreateDirectory(Path.Combine(dir.Dir, "sub"));
        File.WriteAllText(Path.Combine(dir.Dir, "sub", "inner.lua"), "return 42");
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("v = require('sub.inner')");

        Assert.Equal(42.0, shell.GetGlobal("v")!.Number);
    }

    [Fact]
    public void Require_MissingModule_FailsAsRuntimeError()
    {
        using var dir = new TempModuleDir();
        var shell = new LuaShell(new FakeLuaHost());
        var error = CaptureError(() => Assert.Equal(1, shell.Execute("require('no_such_module_xyz')")));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Require_CachesModule()
    {
        using var dir = new TempModuleDir();
        File.WriteAllText(Path.Combine(dir.Dir, "counter.lua"), "loaded = (loaded or 0) + 1 return loaded");
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("a = require('counter') b = require('counter')");

        Assert.Equal(1.0, shell.GetGlobal("a")!.Number);
        Assert.Equal(1.0, shell.GetGlobal("b")!.Number);
    }
}

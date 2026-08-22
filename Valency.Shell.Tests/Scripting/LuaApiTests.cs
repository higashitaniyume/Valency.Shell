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

    public int NextRunCode { get; set; }
    public string NextCaptureOutput { get; set; } = "captured";
    public int NextCaptureCode { get; set; }
    public int NextPipelineCode { get; set; }
    public int? NextSpawnId { get; set; } = 7;
    public List<LuaJob> Jobs { get; } = [];
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

    public IReadOnlyList<LuaJob> GetJobs() => Jobs;

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
    public void Jobs_ReturnsStructuredTable()
    {
        var host = new FakeLuaHost();
        host.Jobs.Add(new LuaJob(1, 10892, "make", "running"));
        var shell = new LuaShell(host);

        shell.Execute("j = jobs() jid = j[1].id cmd = j[1].cmd state = j[1].state");

        Assert.Equal(1.0, shell.GetGlobal("jid")!.Number);
        Assert.Equal("make", shell.GetGlobal("cmd")!.String);
        Assert.Equal("running", shell.GetGlobal("state")!.String);
    }

    // ---- 对象 API：ls / cat / lines / writefile / grep ----

    private sealed class TempWorkDir : IDisposable
    {
        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "valency-obj-" + Guid.NewGuid().ToString("N"));

        public TempWorkDir() => Directory.CreateDirectory(Dir);

        public void Dispose() => Directory.Delete(Dir, recursive: true);
    }

    [Fact]
    public void Ls_ReturnsStructuredEntries()
    {
        using var dir = new TempWorkDir();
        Directory.CreateDirectory(Path.Combine(dir.Dir, "sub"));
        File.WriteAllText(Path.Combine(dir.Dir, "a.txt"), "hello");
        var host = new FakeLuaHost { WorkingDirectory = dir.Dir };
        var shell = new LuaShell(host);

        shell.Execute("""
            entries = ls()
            n = #entries
            first_name = entries[1].name
            first_is_dir = entries[1].is_dir
            file_entry = entries[2]
            file_size = file_entry.size
            """);

        Assert.Equal(2.0, shell.GetGlobal("n")!.Number);
        Assert.Equal("sub", shell.GetGlobal("first_name")!.String); // 目录排前
        Assert.True(shell.GetGlobal("first_is_dir")!.Boolean);
        Assert.Equal("a.txt", shell.GetGlobal("file_entry")!.Table.Get("name").String);
        Assert.Equal(5.0, shell.GetGlobal("file_size")!.Number);
    }

    [Fact]
    public void Ls_MissingDirectory_FailsAsRuntimeError()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var error = CaptureError(() => Assert.Equal(1, shell.Execute("ls('Z:/no/such/dir')")));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Cat_Lines_Writefile_RoundTrip()
    {
        using var dir = new TempWorkDir();
        var host = new FakeLuaHost { WorkingDirectory = dir.Dir };
        var shell = new LuaShell(host);
        var path = (dir.Dir + "/notes.txt").Replace('\\', '/');

        shell.Execute($"""
            ok = writefile("{path}", "one\ntwo\nthree")
            text = cat("{path}")
            l = lines("{path}")
            count = #l
            second = l[2]
            """);

        Assert.True(shell.GetGlobal("ok")!.Boolean);
        Assert.Equal("one\ntwo\nthree", shell.GetGlobal("text")!.String);
        Assert.Equal(3.0, shell.GetGlobal("count")!.Number);
        Assert.Equal("two", shell.GetGlobal("second")!.String);
    }

    [Fact]
    public void Grep_ObjectVersion_FiltersLines()
    {
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("m = grep('er', {'one', 'two er', 'error three'}) n = #m first = m[1]");

        Assert.Equal(2.0, shell.GetGlobal("n")!.Number);
        Assert.Equal("two er", shell.GetGlobal("first")!.String);
    }

    [Fact]
    public void Grep_AcceptsMultilineString()
    {
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("m = grep('x', 'a x\\nb\\nc x') n = #m");

        Assert.Equal(2.0, shell.GetGlobal("n")!.Number);
    }

    // ---- 渲染器（REPL 回显 / echo 渲染） ----

    [Fact]
    public void Echo_ArrayOfTables_RendersAlignedGrid()
    {
        var shell = new LuaShell(new FakeLuaHost());

        var output = CaptureOutput(() => shell.Execute("""
            echo({ { name = "ab", size = 1 }, { name = "cdef", size = 22 } })
            """));

        var lines = output.Split("\r\n");
        Assert.Contains("name  size", lines);    // 列宽由最宽内容（含表头）决定，列间两空格
        Assert.Contains("ab       1", lines);    // 数字列右对齐
        Assert.Contains("cdef    22", lines);
    }

    [Fact]
    public void Echo_MapTable_RendersKeyValueList()
    {
        var shell = new LuaShell(new FakeLuaHost());

        var output = CaptureOutput(() => shell.Execute("echo({ name = 'valency', n = 2 })"));

        Assert.Contains("name : valency", output);
        Assert.Contains("n    : 2", output);
    }

    [Fact]
    public void Echo_ScalarArray_RendersOnePerLine()
    {
        var shell = new LuaShell(new FakeLuaHost());

        var output = CaptureOutput(() => shell.Execute("echo({'a', 'b'})"));

        Assert.Equal($"a{Environment.NewLine}b{Environment.NewLine}", output);
    }

    [Fact]
    public void ReplEcho_Ls_RendersTableWithHeader()
    {
        using var dir = new TempWorkDir();
        File.WriteAllText(Path.Combine(dir.Dir, "only.txt"), "x");
        var host = new FakeLuaHost { WorkingDirectory = dir.Dir };
        var shell = new LuaShell(host);

        var output = CaptureOutput(() => shell.Execute("ls()"));

        Assert.Contains("name", output);
        Assert.Contains("only.txt", output);
        Assert.Contains("is_dir", output);
    }

    [Fact]
    public void Renderer_CapsRows()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var sb = new System.Text.StringBuilder();
        sb.Append("t = {");
        for (var i = 1; i <= 105; i++)
        {
            if (i > 1) sb.Append(", ");
            sb.Append($"{{ n = {i} }}");
        }
        sb.Append('}');
        shell.Execute(sb.ToString());

        var output = CaptureOutput(() => shell.Execute("t"));

        Assert.Contains("…", output); // LuaMoreRows 截断提示
    }

    [Fact]
    public void ReplEcho_Tuple_StaysTabSeparated()
    {
        var shell = new LuaShell(new FakeLuaHost());
        var output = CaptureOutput(() => shell.Execute("1, 'a'"));
        Assert.Equal($"1\ta{Environment.NewLine}", output);
    }

    // ---- 方法链 ----

    [Fact]
    public void Chain_FilterMapSort_OnLsResults()
    {
        using var dir = new TempWorkDir();
        Directory.CreateDirectory(Path.Combine(dir.Dir, "sub"));
        File.WriteAllText(Path.Combine(dir.Dir, "a.lua"), "x");
        File.WriteAllText(Path.Combine(dir.Dir, "b.lua"), "x");
        var host = new FakeLuaHost { WorkingDirectory = dir.Dir };
        var shell = new LuaShell(host);

        shell.Execute("""
            files = ls()
                :filter(function(e) return not e.is_dir end)
                :map(function(e) return e.name end)
                :sort()
            n = #files
            first = files[1]
            """);

        Assert.Equal(2.0, shell.GetGlobal("n")!.Number);
        Assert.Equal("a.lua", shell.GetGlobal("first")!.String);
    }

    [Fact]
    public void Chain_ReverseTake_OnGrepResults()
    {
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("m = grep('a', {'a1', 'b', 'a2'}):reverse():take(1) n = #m only = m[1]");

        Assert.Equal(1.0, shell.GetGlobal("n")!.Number);
        Assert.Equal("a2", shell.GetGlobal("only")!.String);
    }

    [Fact]
    public void Chain_SortWithComparator_Descending()
    {
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("m = grep('x', {'x1', 'x3', 'x2'}):sort(function(a, b) return a > b end) first = m[1]");

        Assert.Equal("x3", shell.GetGlobal("first")!.String);
    }

    [Fact]
    public void Chain_Echo_PrintsRenderedResult()
    {
        var shell = new LuaShell(new FakeLuaHost());

        var output = CaptureOutput(() => shell.Execute("grep('a', {'ab', 'cd'}):echo()"));

        Assert.Equal($"ab{Environment.NewLine}", output);
    }

    [Fact]
    public void Chain_PlainTableSemanticsPreserved()
    {
        var shell = new LuaShell(new FakeLuaHost());

        shell.Execute("""
            m = grep('a', {'a1', 'a2'})
            kind = type(m)
            n = #m
            seen = 0
            for _, v in ipairs(m) do seen = seen + 1 end
            """);

        Assert.Equal("table", shell.GetGlobal("kind")!.String);
        Assert.Equal(2.0, shell.GetGlobal("n")!.Number);
        Assert.Equal(2.0, shell.GetGlobal("seen")!.Number);
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
    public void Env_ReplEcho_RendersSnapshot()
    {
        var shell = new LuaShell(new FakeLuaHost());
        Environment.SetEnvironmentVariable("VALENCY_ENV_RENDER_T", "42");

        var output = CaptureOutput(() => shell.Execute("env"));

        Assert.Contains("VALENCY_ENV_RENDER_T", output);   // key 有列对齐填充
        Assert.Contains(": 42", output);
        Assert.Contains("PATH", output);
        Environment.SetEnvironmentVariable("VALENCY_ENV_RENDER_T", null);
    }

    [Fact]
    public void Env_Pairs_IteratesProcessEnvironment()
    {
        var shell = new LuaShell(new FakeLuaHost());
        Environment.SetEnvironmentVariable("VALENCY_PAIRS_T", "x");

        shell.Execute("found = false for k, v in pairs(env) do if k == 'VALENCY_PAIRS_T' then found = (v == 'x') end end");

        Assert.True(shell.GetGlobal("found")!.Boolean);
        Environment.SetEnvironmentVariable("VALENCY_PAIRS_T", null);
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

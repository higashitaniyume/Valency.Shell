using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Loaders;
using Serilog;
using Valency.Shell.Scripting.Expansion;

namespace Valency.Shell.Scripting.Lua;

/// <summary>
///     The Lua language layer: a MoonSharp script whose globals expose the shell API
///     (run/capture/pipe/spawn/glob/exit/status/env/args) plus transparent command
///     proxies for builtins and PATH executables. Any valid Lua runs here.
/// </summary>
public sealed class LuaShell
{
    private sealed class ExitRequestedException(int code) : Exception
    {
        public int Code { get; } = code;
    }

    private readonly Script _script;
    private readonly ILuaHost _host;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, DynValue> _proxies = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownCommands = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressEcho;

    public LuaShell(ILuaHost host, ILogger? logger = null)
    {
        _host = host;
        _logger = logger?.ForContext("Src", "lua");
        _script = new Script();
        ConfigureScriptLoader();
        RegisterApi();
    }

    /// <summary>
    ///     require() 解析：当前目录 ./?.lua、./?/init.lua，用户库目录 ~/.valency/lua/，
    ///     以及 VALENCY_LUA_PATH（分号分隔，支持 ? 模板；纯目录自动补 /?.lua）。
    /// </summary>
    private void ConfigureScriptLoader()
    {
        var loader = new FileSystemScriptLoader { ModulePaths = BuildModulePaths() };
        _script.Options.ScriptLoader = loader;
    }

    private static string[] BuildModulePaths()
    {
        var paths = new List<string> { "?.lua", "?/init.lua" };

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            var lib = home.Replace('\\', '/') + "/.valency/lua";
            paths.Add(lib + "/?.lua");
            paths.Add(lib + "/?/init.lua");
        }

        var extra = Environment.GetEnvironmentVariable("VALENCY_LUA_PATH");
        if (!string.IsNullOrEmpty(extra))
        {
            foreach (var entry in extra.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var template = entry.Contains('?') ? entry.Replace('\\', '/') : entry.Replace('\\', '/') + "/?.lua";
                paths.Add(template);
            }
        }

        return [.. paths];
    }

    public Table Globals => _script.Globals;

    public DynValue? GetGlobal(string name)
    {
        var value = _script.Globals.Get(name);
        return value.IsNil() ? null : value;
    }

    public string? GetGlobalString(string name)
    {
        var value = _script.Globals.Get(name);
        if (value.IsNil())
            return null;
        return value.Type == DataType.String ? value.String : value.ToPrintString();
    }

    public void SetGlobal(string name, string value) =>
        _script.Globals.Set(name, DynValue.NewString(value));

    public void UnsetGlobal(string name) => _script.Globals.Set(name, DynValue.Nil);

    public void SetScriptArgs(string scriptName, IReadOnlyList<string> positional)
    {
        var args = DynValue.NewTable(_script);
        args.Table.Set(DynValue.NewNumber(0), DynValue.NewString(scriptName));
        for (var i = 0; i < positional.Count; i++)
            args.Table.Set(DynValue.NewNumber(i + 1), DynValue.NewString(positional[i]));
        _script.Globals.Set("args", args);
    }

    /// <summary>REPL line: try `return (text)` first so expressions echo their value, else run as a chunk.</summary>
    public int Execute(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return _host.LastExitCode;

        DynValue chunk;
        var echo = false;
        try
        {
            chunk = _script.LoadString("return " + text, null, "stdin");
            echo = true;
        }
        catch (SyntaxErrorException)
        {
            try
            {
                chunk = _script.LoadString(text, null, "stdin");
            }
            catch (SyntaxErrorException ex)
            {
                Console.Error.WriteLine(ex.DecoratedMessage);
                _logger?.Error(Resources.LogLuaSyntaxError, ex.DecoratedMessage);
                return 2;
            }
        }

        return CallChunk(chunk, echo);
    }

    /// <summary>Script file body: run as a plain chunk, no expression echo.</summary>
    public int ExecuteFile(string name, string code)
    {
        DynValue chunk;
        try
        {
            chunk = _script.LoadString(code, null, name);
        }
        catch (SyntaxErrorException ex)
        {
            Console.Error.WriteLine(ex.DecoratedMessage);
            _logger?.Error(Resources.LogLuaSyntaxError, ex.DecoratedMessage);
            return 2;
        }
        return CallChunk(chunk, echo: false);
    }

    private int CallChunk(DynValue chunk, bool echo)
    {
        _logger?.Debug(Resources.LogLuaChunk, echo ? "return " : "chunk");
        _suppressEcho = false;
        try
        {
            var result = _script.Call(chunk);
            // 命令类调用（run/代理/pipe/spawn）返回退出码，不作为表达式结果回显
            if (echo && !_suppressEcho)
                PrintResult(result);
            return _host.LastExitCode;
        }
        catch (ExitRequestedException ex)
        {
            return ex.Code;
        }
        catch (ScriptRuntimeException ex) when (ex.InnerException is ExitRequestedException exit)
        {
            return exit.Code;
        }
        catch (ScriptRuntimeException ex)
        {
            Console.Error.WriteLine(ex.DecoratedMessage);
            _logger?.Error(Resources.LogLuaRuntimeError, ex.DecoratedMessage);
            return 1;
        }
    }

    private static void PrintResult(DynValue result)
    {
        if (result.IsNil())
            return;

        var text = LuaRenderer.Render(result);
        if (text.Length > 0)
            Console.Out.WriteLine(text);
    }

    // ---- API registration ----

    private void RegisterApi()
    {
        var globals = _script.Globals;
        globals.Set("run", DynValue.NewCallback(new CallbackFunction(RunCallback, "run")));
        globals.Set("capture", DynValue.NewCallback(new CallbackFunction(CaptureCallback, "capture")));
        globals.Set("pipe", DynValue.NewCallback(new CallbackFunction(PipeCallback, "pipe")));
        globals.Set("spawn", DynValue.NewCallback(new CallbackFunction(SpawnCallback, "spawn")));
        globals.Set("glob", DynValue.NewCallback(new CallbackFunction(GlobCallback, "glob")));
        globals.Set("exit", DynValue.NewCallback(new CallbackFunction(ExitCallback, "exit")));
        globals.Set("status", DynValue.NewCallback(new CallbackFunction(StatusCallback, "status")));
        // 原生 echo：参数经 LuaRenderer 渲染（echo(ls()) 打印表格）；覆盖命令代理
        globals.Set("echo", DynValue.NewCallback(new CallbackFunction(EchoCallback, "echo")));
        ObjectApi.Register(_script, _host);
        RegisterEnvTable();
        RegisterCommandProxy();
    }

    private static DynValue EchoCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        var parts = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].IsNil())
                continue;
            var rendered = LuaRenderer.Render(args[i]);
            if (rendered.Length > 0)
                parts.Add(rendered);
        }
        Console.Out.WriteLine(string.Join(" ", parts));
        return DynValue.Nil;
    }

    private DynValue RunCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        _suppressEcho = true;
        var code = RunArgv(LuaMarshaling.ToArgv(args, 0));
        return DynValue.NewNumber(code);
    }

    private int RunArgv((List<string> Argv, DynValue? Options) marshaled)
    {
        var (argv, options) = marshaled;
        if (argv.Count == 0)
            throw Errors.MissingCommand();
        var code = _host.Run(argv, LuaMarshaling.ToRedirects(options));
        ThrowIfExitRequested();
        return code;
    }

    private DynValue CaptureCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        var (argv, _) = LuaMarshaling.ToArgv(args, 0);
        if (argv.Count == 0)
            throw Errors.MissingCommand();

        var captured = _host.Capture(argv);
        ThrowIfExitRequested();
        return DynValue.NewTuple(
            DynValue.NewString(captured.Output.TrimEnd('\r', '\n')),
            DynValue.NewNumber(captured.ExitCode));
    }

    private DynValue PipeCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        var stages = new List<string[]>();
        DynValue? options = null;
        for (var i = 0; i < args.Count; i++)
        {
            var value = args[i];
            if (options is null && LuaMarshaling.IsOptionsTable(value))
            {
                options = value;
                continue;
            }
            stages.Add(ToStage(value, stages.Count + 1));
        }

        if (stages.Count == 0)
            throw Errors.MissingCommand();

        _suppressEcho = true;
        var code = _host.Pipeline(stages, LuaMarshaling.ToRedirects(options));
        ThrowIfExitRequested();
        return DynValue.NewNumber(code);
    }

    internal static string[] ToStage(DynValue value, int index)
    {
        if (value.Type == DataType.String)
        {
            var words = value.String.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 0 ? words : throw Errors.PipeStageEmpty(index);
        }

        if (value.Type == DataType.Table)
        {
            var argv = new List<string>();
            for (var i = 1; i <= value.Table.Length; i++)
                LuaMarshaling.AppendValue(argv, value.Table.Get(i), index);
            return argv.Count > 0 ? [.. argv] : throw Errors.PipeStageEmpty(index);
        }

        throw Errors.PipeStageForm(index);
    }

    private DynValue SpawnCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        var (argv, _) = LuaMarshaling.ToArgv(args, 0);
        if (argv.Count == 0)
            throw Errors.MissingCommand();

        var jobId = _host.Spawn(argv);
        ThrowIfExitRequested();
        _suppressEcho = true;
        return jobId is null ? DynValue.Nil : DynValue.NewNumber(jobId.Value);
    }

    private static DynValue GlobCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (args.Count < 1 || args[0].Type != DataType.String)
            throw Errors.MissingCommand();

        var matches = GlobExpander.Expand(args[0].String);
        var table = DynValue.NewTable(ctx.OwnerScript);
        for (var i = 0; i < matches.Count; i++)
            table.Table.Set(DynValue.NewNumber(i + 1), DynValue.NewString(matches[i]));
        return LuaQuery.Wrap(ctx.OwnerScript, table);
    }

    private DynValue ExitCallback(ScriptExecutionContext ctx, CallbackArguments args)
    {
        var code = args.Count > 0 && args[0].Type == DataType.Number ? (int)args[0].Number : 0;
        _host.RequestExit(code);
        throw new ExitRequestedException(code);
    }

    private DynValue StatusCallback(ScriptExecutionContext ctx, CallbackArguments args) =>
        DynValue.NewNumber(_host.LastExitCode);

    private void ThrowIfExitRequested()
    {
        if (_host.ExitRequested)
            throw new ExitRequestedException(_host.RequestedExitCode);
    }

    private void RegisterEnvTable()
    {
        var env = DynValue.NewTable(_script);
        var meta = DynValue.NewTable(_script);
        meta.Table.Set("__index", DynValue.NewCallback(new CallbackFunction((ctx, args) =>
        {
            var key = args[args.Count - 1].String;
            if (key is null)
                return DynValue.Nil;
            var value = Environment.GetEnvironmentVariable(key);
            return value is null ? DynValue.Nil : DynValue.NewString(value);
        }, "env.get")));
        meta.Table.Set("__newindex", DynValue.NewCallback(new CallbackFunction((ctx, args) =>
        {
            var key = args[1].String;
            if (key is null)
                return DynValue.Nil;
            var value = args[2];
            Environment.SetEnvironmentVariable(key, value.IsNil() ? null : value.ToPrintString());
            return DynValue.Nil;
        }, "env.set")));
        env.Table.MetaTable = meta.Table;
        _script.Globals.Set("env", env);
    }

    private void RegisterCommandProxy()
    {
        var meta = DynValue.NewTable(_script);
        meta.Table.Set("__index", DynValue.NewCallback(new CallbackFunction((ctx, args) =>
        {
            var key = args[args.Count - 1].String;
            if (key is null)
                return DynValue.Nil;
            if (!_knownCommands.Contains(key) && !_host.IsCommandAvailable(key))
                return DynValue.Nil;
            _knownCommands.Add(key);
            return GetOrCreateProxy(key);
        }, "command")));
        _script.Globals.MetaTable = meta.Table;
    }

    private DynValue GetOrCreateProxy(string name)
    {
        if (_proxies.TryGetValue(name, out var proxy))
            return proxy;

        proxy = DynValue.NewCallback(new CallbackFunction((ctx, args) =>
        {
            var (rest, options) = LuaMarshaling.ToArgv(args, 0);
            var argv = new List<string>(capacity: rest.Count + 1) { name };
            argv.AddRange(rest);
            var code = _host.Run(argv, LuaMarshaling.ToRedirects(options));
            ThrowIfExitRequested();
            _suppressEcho = true;
            return DynValue.NewNumber(code);
        }, name));
        _proxies[name] = proxy;
        return proxy;
    }
}

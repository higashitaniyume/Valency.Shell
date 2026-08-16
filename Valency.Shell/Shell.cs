using System.Diagnostics;
using Serilog;
using Valency.Shell.Builtins;
using Valency.Shell.Core.Completion;
using Valency.Shell.Editing;
using Valency.Shell.Engine;
using Valency.Shell.Prompting;
using Valency.Shell.Scripting.Ast;
using Valency.Shell.Scripting.Eval;
using Valency.Shell.Scripting.Lexing;
using Valency.Shell.Scripting.Parsing;

namespace Valency.Shell;

public sealed class CommandCompletedEventArgs(string rawCommand, int exitCode) : EventArgs
{
    public string RawCommand { get; } = rawCommand;
    public int ExitCode { get; } = exitCode;
}

public sealed class JobEventArgs(BackgroundJob job) : EventArgs
{
    public BackgroundJob Job { get; } = job;
}

public sealed class Shell : IShellContext, IShellRuntime
{
    private readonly LineEditor _editor;
    private readonly ShellState _state = new();
    private readonly Interpreter _interpreter;
    private readonly BuiltinRegistry _builtins;
    private readonly ILogger _logger;
    private readonly PromptFormatter _promptFormatter;
    private readonly PromptSettings _promptSettings;
    private readonly List<BackgroundJob> _jobs = new();
    private readonly List<Process> _foreground = new();
    private int _nextJobId = 1;
    private TextReader? _pipelineInput;

    public bool IsRunning { get; private set; }

    public event EventHandler<CommandCompletedEventArgs>? CommandCompleted;
    public event EventHandler<JobEventArgs>? JobStarted;
    public event EventHandler<JobEventArgs>? JobCompleted;

    public Shell(BuiltinRegistry builtins, ILogger logger, PromptFormatter promptFormatter, PromptSettings promptSettings)
    {
        _builtins = builtins;
        _logger = logger.ForContext<Shell>();
        _promptFormatter = promptFormatter;
        _promptSettings = promptSettings;
        _interpreter = new Interpreter(this, _state);
        _editor = new LineEditor(new CompletionEngine(builtins.Commands.Select(c => c.Spec.Name)));
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public string ScriptName
    {
        get => _state.ScriptName;
        set => _state.ScriptName = value;
    }

    public IReadOnlyList<string> PositionalArgs
    {
        get => _state.PositionalArgs;
        set => _state.PositionalArgs = value;
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _logger.Warning("收到 Ctrl+C，中断前台进程");
        lock (_foreground)
        {
            foreach (var process in _foreground)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    public int Run()
    {
        IsRunning = true;
        try
        {
            while (true)
            {
                ReapJobs();

                var result = _editor.ReadLine(_promptSettings.Build(_promptFormatter));
                if (result.Kind == LineResultKind.Exit)
                    return _state.LastExitCode;
                if (result.Kind == LineResultKind.Cancelled)
                {
                    _state.LastExitCode = 1;
                    continue;
                }

                ExecuteLine(result.Text);
                if (_state.ExitRequested)
                    return _state.ExitCode;
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    public int RunScript(TextReader reader)
    {
        IsRunning = true;
        try
        {
            return ExecuteScript(reader.ReadToEnd());
        }
        finally
        {
            IsRunning = false;
        }
    }

    public int ExecuteLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return _state.LastExitCode;

        var code = ExecuteScript(line);
        CommandCompleted?.Invoke(this, new CommandCompletedEventArgs(line, code));
        return code;
    }

    private int ExecuteScript(string text)
    {
        try
        {
            return _interpreter.Execute(text);
        }
        catch (SyntaxError ex)
        {
            Console.Error.WriteLine(ex.Message);
            _logger.Error(ex, "解析失败: {Text}", text);
            _state.LastExitCode = 2;
            return 2;
        }
        catch (IncompleteInputException ex)
        {
            Console.Error.WriteLine(ex.Message);
            _state.LastExitCode = 2;
            return 2;
        }
    }

    // ---- IShellRuntime ----

    public int ExecuteSimpleCommand(IReadOnlyList<string> argv, IReadOnlyList<ResolvedRedirection> redirects)
    {
        if (argv.Count == 0)
            return 0;

        if (_builtins.TryGet(argv[0], out var builtin))
            return ExecuteBuiltin(builtin, argv);

        var specs = TranslateRedirects(redirects);
        var code = ProcessRunner.Run(argv[0], argv.Skip(1).ToArray(), specs, _state.CurrentDirectory, _logger);
        _state.LastExitCode = code;
        return code;
    }

    public int ExecutePipeline(IReadOnlyList<PipelineStage> stages)
    {
        if (stages.Count == 1)
            return ExecuteSimpleCommand(stages[0].Argv, stages[0].Redirects);

        if (_builtins.TryGet(stages[^1].Argv[0], out var lastBuiltin))
        {
            var external = stages.Take(stages.Count - 1)
                .Select(s => s.Argv.ToArray())
                .ToArray();
            var captured = ProcessRunner.RunPipelineCaptured(
                external, _foreground, _logger, out var pipelineExit, _state.CurrentDirectory);
            _state.LastExitCode = pipelineExit;
            _pipelineInput = new StringReader(captured);
            try
            {
                return ExecuteBuiltin(lastBuiltin, stages[^1].Argv);
            }
            finally
            {
                _pipelineInput = null;
            }
        }

        var commands = stages.Select(s => s.Argv.ToArray()).ToArray();
        var redirectSpecs = new List<RedirectSpec>();
        foreach (var r in stages[0].Redirects)
            redirectSpecs.AddRange(TranslateOne(r));
        foreach (var r in stages[^1].Redirects)
            redirectSpecs.AddRange(TranslateOne(r));

        var code = ProcessRunner.RunPipeline(commands, _foreground, redirectSpecs, _state.CurrentDirectory, _logger);
        _state.LastExitCode = code;
        return code;
    }

    public int ExecuteBackground(IReadOnlyList<string> argv)
    {
        if (argv.Count == 0)
            return 0;

        if (_builtins.TryGet(argv[0], out var builtin))
            return ExecuteBuiltin(builtin, argv);

        var job = ProcessRunner.StartBackground(argv[0], argv.Skip(1).ToArray(), _logger, _state.CurrentDirectory);
        if (job is null)
        {
            _state.LastExitCode = 127;
            return 127;
        }

        job.Id = _nextJobId++;
        _state.LastBackgroundPid = job.Process.Id;
        _jobs.Add(job);
        Console.Out.WriteLine($"[{job.Id}] {job.Process.Id}");
        _logger.Information("作业启动 [{JobId}] {Command} (PID {Pid})", job.Id, job.Command, job.Process.Id);
        JobStarted?.Invoke(this, new JobEventArgs(job));
        return 0;
    }

    private int ExecuteBuiltin(IBuiltinCommand builtin, IReadOnlyList<string> argv)
    {
        ParseResult? parseResult;
        if (builtin.Spec.RawArgs)
        {
            parseResult = new ParseResult();
            foreach (var arg in argv.Skip(1))
                parseResult.Positionals.Add(arg);
        }
        else
        {
            parseResult = ArgParser.Parse(argv.Skip(1).ToArray(), builtin.Spec, out var error);
            if (parseResult is null)
            {
                Console.Error.WriteLine($"{argv[0]}: {error}");
                _state.LastExitCode = 2;
                return 2;
            }

            if (parseResult.HelpRequested)
            {
                HelpRenderer.PrintCommand(builtin.Spec);
                _state.LastExitCode = 0;
                return 0;
            }
        }

        var code = builtin.Execute(parseResult, this);
        _state.LastExitCode = code;
        return code;
    }

    private static IReadOnlyList<RedirectSpec> TranslateRedirects(IReadOnlyList<ResolvedRedirection> redirects)
    {
        var list = new List<RedirectSpec>();
        foreach (var redirect in redirects)
            list.AddRange(TranslateOne(redirect));
        return list;
    }

    private static IReadOnlyList<RedirectSpec> TranslateOne(ResolvedRedirection redirect)
    {
        return redirect.Kind switch
        {
            RedirectionKind.Input => [new RedirectSpec(redirect.Fd, RedirectKind.Input, redirect.Target)],
            RedirectionKind.Output => [new RedirectSpec(redirect.Fd, RedirectKind.Output, redirect.Target)],
            RedirectionKind.Append => [new RedirectSpec(redirect.Fd, RedirectKind.Append, redirect.Target)],
            RedirectionKind.DupOutput => [new RedirectSpec(redirect.Fd, RedirectKind.DupOutput, redirect.Target)],
            RedirectionKind.AndOutput =>
            [
                new RedirectSpec(1, RedirectKind.Output, redirect.Target),
                new RedirectSpec(2, RedirectKind.Output, redirect.Target),
            ],
            RedirectionKind.AndAppend =>
            [
                new RedirectSpec(1, RedirectKind.Append, redirect.Target),
                new RedirectSpec(2, RedirectKind.Append, redirect.Target),
            ],
            _ => [],
        };
    }

    // ---- IShellContext ----

    public int LastExitCode
    {
        get => _state.LastExitCode;
        set => _state.LastExitCode = value;
    }

    public string? PreviousDirectory { get; set; }

    public string CurrentDirectory
    {
        get => _state.CurrentDirectory;
        set => _state.CurrentDirectory = value;
    }

    public bool ExitRequested => _state.ExitRequested;
    public int RequestedExitCode => _state.ExitCode;
    public void RequestExit(int exitCode) => _state.ExitCode = exitCode;

    public void PrintJobs() => ListJobs();

    public TextReader? PipelineInput => _pipelineInput;

    public string? GetVariable(string name) => _state.GetVariable(name);

    public void SetVariable(string name, string value, bool exported) => _state.SetVariable(name, value, exported);

    public void ExportVariable(string name) => _state.ExportVariable(name);

    public void UnsetVariable(string name) => _state.UnsetVariable(name);

    public void ShiftArguments(int count)
    {
        var skip = Math.Min(count, _state.PositionalArgs.Count);
        _state.PositionalArgs = _state.PositionalArgs.Skip(skip).ToArray();
    }

    public int RunScriptFile(string path)
    {
        var full = Path.GetFullPath(path, _state.CurrentDirectory);
        if (!File.Exists(full))
        {
            Console.Error.WriteLine($"source: 文件不存在: {path}");
            return 1;
        }
        return ExecuteScript(File.ReadAllText(full));
    }

    private void ReapJobs()
    {
        for (var i = _jobs.Count - 1; i >= 0; i--)
        {
            var job = _jobs[i];
            if (!job.TryComplete())
                continue;

            job.Output.Wait();
            job.Error.Wait();
            var output = job.Output.Result + job.Error.Result;

            Console.Out.WriteLine($"[{job.Id}] Done ({job.ExitCode})");
            if (output.Length > 0)
                Console.Out.Write(output);

            _logger.Information("作业完成 [{JobId}] 退出码 {ExitCode}", job.Id, job.ExitCode);
            job.Process.Dispose();
            _jobs.RemoveAt(i);
            JobCompleted?.Invoke(this, new JobEventArgs(job));
        }
    }

    private void ListJobs()
    {
        lock (_jobs)
        {
            foreach (var job in _jobs.Where(j => j.State == BackgroundJobState.Running))
                Console.Out.WriteLine($"[{job.Id}] 运行中  {job.Command} (PID {job.Process.Id})");
        }
    }
}

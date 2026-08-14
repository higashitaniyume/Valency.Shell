using System.Diagnostics;
using Valency.Shell.Builtins;
using Valency.Shell.Core.Expansion;
using Valency.Shell.Core.Syntax;
using Valency.Shell.Editing;
using Valency.Shell.Engine;

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

public sealed class Shell : IShellContext
{
    private readonly LineEditor _editor = new();
    private readonly VariableExpander _expander;
    private readonly BuiltinRegistry _builtins;
    private readonly List<BackgroundJob> _jobs = new();
    private readonly List<Process> _foreground = new();
    private int _nextJobId = 1;
    private bool _exitRequested;
    private int _requestedExitCode;

    public int LastExitCode { get; set; }
    public string? PreviousDirectory { get; set; }
    public bool IsRunning { get; private set; }

    public event EventHandler<CommandCompletedEventArgs>? CommandCompleted;
    public event EventHandler<JobEventArgs>? JobStarted;
    public event EventHandler<JobEventArgs>? JobCompleted;

    public Shell(BuiltinRegistry builtins)
    {
        _builtins = builtins;
        _expander = new VariableExpander(new ShellVariableSource(this));
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    private sealed class ShellVariableSource(Shell shell) : IVariableSource
    {
        public bool TryGet(string name, out string? value)
        {
            if (name == "?")
            {
                value = shell.LastExitCode.ToString();
                return true;
            }
            value = Environment.GetEnvironmentVariable(name);
            return value is not null;
        }
    }

    bool IShellContext.ExitRequested => _exitRequested;
    int IShellContext.RequestedExitCode => _requestedExitCode;

    void IShellContext.RequestExit(int exitCode)
    {
        _requestedExitCode = exitCode;
        _exitRequested = true;
    }

    void IShellContext.PrintJobs() => ListJobs();

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
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

                var result = _editor.ReadLine($"valency {Environment.CurrentDirectory}> ");
                if (result.Kind == LineResultKind.Exit)
                    return LastExitCode;
                if (result.Kind == LineResultKind.Cancelled)
                {
                    LastExitCode = 1;
                    continue;
                }

                List<ParsedCommand> parsed;
                try
                {
                    parsed = LineParser.Parse(result.Text);
                }
                catch (FormatException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    LastExitCode = 2;
                    continue;
                }

                var runNext = true;
                var i = 0;
                while (i < parsed.Count)
                {
                    var group = new List<string[]>();
                    var groupRaw = new List<string>();
                    var connector = parsed[i].Connector;
                    group.Add(ExpandCommand(parsed[i].RawText));
                    groupRaw.Add(parsed[i].RawText);

                    while (connector == Connector.Pipe && i + 1 < parsed.Count)
                    {
                        i++;
                        connector = parsed[i].Connector;
                        group.Add(ExpandCommand(parsed[i].RawText));
                        groupRaw.Add(parsed[i].RawText);
                    }

                    if (connector == Connector.Pipe)
                    {
                        Console.Error.WriteLine("管道 '|' 后缺少命令");
                        LastExitCode = 2;
                        break;
                    }

                    i++;
                    var rawCommand = string.Join(" | ", groupRaw);
                    var background = connector == Connector.Background && group.Count == 1;
                    var effective = connector == Connector.Background ? Connector.Semicolon : connector;

                    if (runNext)
                    {
                        if (background)
                        {
                            StartBackground(group[0]);
                            runNext = RunNext(executed: true, effective, 0);
                        }
                        else
                        {
                            var code = ExecuteGroup(group, out var exitSignal);
                            CommandCompleted?.Invoke(this, new CommandCompletedEventArgs(rawCommand, LastExitCode));
                            if (exitSignal)
                                return LastExitCode;
                            runNext = RunNext(executed: true, effective, code);
                        }
                    }
                    else
                    {
                        runNext = RunNext(executed: false, effective, 0);
                    }
                }
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    public static bool RunNext(bool executed, Connector connector, int code)
    {
        if (!executed)
            return connector switch
            {
                Connector.And => false,
                Connector.Or => true,
                _ => true,
            };

        return connector switch
        {
            Connector.And => code == 0,
            Connector.Or => code != 0,
            _ => true,
        };
    }

    private string[] ExpandCommand(string rawText)
    {
        return CommandParser.SplitTokens(rawText)
            .Select(t => _expander.Expand(t))
            .ToArray();
    }

    private int ExecuteGroup(IReadOnlyList<string[]> commands, out bool exitSignal)
    {
        exitSignal = false;

        if (commands.Count == 1)
            return ExecuteSingle(commands[0], out exitSignal);

        foreach (var command in commands)
        {
            if (_builtins.TryGet(command[0], out _))
            {
                Console.Error.WriteLine("内置命令不支持管道");
                return 2;
            }
        }

        LastExitCode = ProcessRunner.RunPipeline(commands, _foreground);
        return LastExitCode;
    }

    private int ExecuteSingle(string[] args, out bool exitSignal)
    {
        exitSignal = false;

        if (_builtins.TryGet(args[0], out var builtin))
        {
            var code = builtin.Execute(args, this);
            if (_exitRequested)
            {
                exitSignal = true;
                LastExitCode = _requestedExitCode;
                return LastExitCode;
            }
            LastExitCode = code;
            return code;
        }

        LastExitCode = ProcessRunner.Run(args[0], args.Skip(1).ToArray());
        return LastExitCode;
    }

    private void StartBackground(string[] args)
    {
        if (_builtins.TryGet(args[0], out _))
        {
            ExecuteSingle(args, out _);
            return;
        }

        var job = ProcessRunner.StartBackground(args[0], args.Skip(1).ToArray());
        if (job is null)
        {
            LastExitCode = 127;
            return;
        }

        job.Id = _nextJobId++;
        _jobs.Add(job);
        Console.Out.WriteLine($"[{job.Id}] {job.Process.Id}");
        JobStarted?.Invoke(this, new JobEventArgs(job));
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

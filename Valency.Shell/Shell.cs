using System.Diagnostics;
using Serilog;
using Valency.Shell.Builtins;
using Valency.Shell.Core.Completion;
using Valency.Shell.Core.Resolution;
using Valency.Shell.Editing;
using Valency.Shell.Engine;
using Valency.Shell.Prompting;
using Valency.Shell.Scripting.Lua;

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

public sealed class Shell : IShellContext, ILuaHost
{
	private readonly LineEditor _editor;
	private readonly LuaShell _lua;
	private readonly BuiltinRegistry _builtins;
	private readonly ILogger _logger;
	private readonly PromptFormatter _promptFormatter;
	private readonly PromptSettings _promptSettings;
	private readonly List<BackgroundJob> _jobs = new();
	private readonly List<Process> _foreground = new();
	private int _nextJobId = 1;
	private TextReader? _pipelineInput;
	private string _scriptName = "valency";
	private IReadOnlyList<string> _positionalArgs = [];
	private int _lastExitCode;
	private long _lastBackgroundPid;
	private bool _exitRequested;
	private int _exitCode;
	private string _currentDirectory = Environment.CurrentDirectory;

	public bool IsRunning { get; private set; }

	public event EventHandler<CommandCompletedEventArgs>? CommandCompleted;
	public event EventHandler<JobEventArgs>? JobStarted;
	public event EventHandler<JobEventArgs>? JobCompleted;

	public Shell(BuiltinRegistry builtins, ILogger logger, PromptFormatter promptFormatter, PromptSettings promptSettings)
	{
		_builtins = builtins;
		_logger = logger.ForContext("Src", "shell");
		_promptFormatter = promptFormatter;
		_promptSettings = promptSettings;
		_lua = new LuaShell(this, _logger);
		_editor = new LineEditor(new CompletionEngine(builtins.Commands.Select(c => c.Spec.Name)));
		Console.CancelKeyPress += OnCancelKeyPress;
	}

	public string ScriptName
	{
		get => _scriptName;
		set
		{
			_scriptName = value;
			_lua.SetScriptArgs(value, _positionalArgs);
		}
	}

	public IReadOnlyList<string> PositionalArgs
	{
		get => _positionalArgs;
		set
		{
			_positionalArgs = value;
			_lua.SetScriptArgs(_scriptName, value);
		}
	}

	private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
	{
		e.Cancel = true;
		_logger.Warning(Resources.LogCtrlCInterrupted);
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
					return _lastExitCode;
				if (result.Kind == LineResultKind.Cancelled)
				{
					_lastExitCode = 1;
					continue;
				}

				ExecuteLine(result.Text);
				if (_exitRequested)
					return _exitCode;
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
			return _lastExitCode;

		var code = ExecuteScript(line);
		CommandCompleted?.Invoke(this, new CommandCompletedEventArgs(line, code));
		return code;
	}

	private int ExecuteScript(string text) => _lua.Execute(text);

	// ---- ILuaHost ----

	private static readonly string[] ScriptExtensions = [".vsh", ".sh", ".bash", ".zsh", ".lua"];

	public int Run(IReadOnlyList<string> argv, IReadOnlyList<LuaRedirect>? redirects)
	{
		if (argv.Count == 0)
			return 0;

		if (_builtins.TryGet(argv[0], out var builtin))
			return ExecuteBuiltin(builtin, argv);

		if (IsScriptFile(argv[0]))
		{
			var code = ExecuteScriptFile(argv[0], argv);
			_lastExitCode = code;
			return code;
		}

		var exit = ProcessRunner.Run(
			argv[0], argv.Skip(1).ToArray(), TranslateLuaRedirects(redirects), _currentDirectory, _logger);
		_lastExitCode = exit;
		return exit;
	}

	public CaptureResult Capture(IReadOnlyList<string> argv)
	{
		if (argv.Count == 0)
			return new CaptureResult(string.Empty, 0);

		if (_builtins.TryGet(argv[0], out var builtin))
			return CaptureWithConsoleSwap(() => ExecuteBuiltin(builtin, argv));

		if (IsScriptFile(argv[0]))
			return CaptureWithConsoleSwap(() => ExecuteScriptFile(argv[0], argv));

		var output = ProcessRunner.RunPipelineCaptured(
			[argv.ToArray()], _foreground, _logger, out var exit, _currentDirectory);
		_lastExitCode = exit;
		return new CaptureResult(output, exit);
	}

	private CaptureResult CaptureWithConsoleSwap(Func<int> execute)
	{
		var originalOut = Console.Out;
		var originalErr = Console.Error;
		var writer = new StringWriter();
		Console.SetOut(writer);
		Console.SetError(writer);
		try
		{
			var code = execute();
			return new CaptureResult(writer.ToString(), code);
		}
		finally
		{
			Console.SetOut(originalOut);
			Console.SetError(originalErr);
		}
	}

	public int Pipeline(IReadOnlyList<string[]> stages, IReadOnlyList<LuaRedirect>? redirects)
	{
		if (stages.Count == 1)
			return Run(stages[0], redirects);

		if (_builtins.TryGet(stages[^1][0], out var lastBuiltin))
		{
			var captured = CaptureExternalPrefix(stages, out var prefixExit);
			_lastExitCode = prefixExit;
			_pipelineInput = new StringReader(captured);
			try
			{
				return ExecuteBuiltin(lastBuiltin, stages[^1]);
			}
			finally
			{
				_pipelineInput = null;
			}
		}

		var specs = TranslateLuaRedirects(redirects);
		var code = ProcessRunner.RunPipeline(stages, _foreground, specs, _currentDirectory, _logger);
		_lastExitCode = code;
		return code;
	}

	public CaptureResult CapturePipeline(IReadOnlyList<string[]> stages)
	{
		if (stages.Count == 1)
			return Capture(stages[0]);

		if (_builtins.TryGet(stages[^1][0], out var lastBuiltin))
		{
			var captured = CaptureExternalPrefix(stages, out var prefixExit);
			_lastExitCode = prefixExit;
			_pipelineInput = new StringReader(captured);
			try
			{
				return CaptureWithConsoleSwap(() => ExecuteBuiltin(lastBuiltin, stages[^1]));
			}
			finally
			{
				_pipelineInput = null;
			}
		}

		var output = ProcessRunner.RunPipelineCaptured(stages, _foreground, _logger, out var exit, _currentDirectory);
		_lastExitCode = exit;
		return new CaptureResult(output, exit);
	}

	private string CaptureExternalPrefix(IReadOnlyList<string[]> stages, out int exitCode)
	{
		var external = stages.Take(stages.Count - 1).ToArray();
		return ProcessRunner.RunPipelineCaptured(external, _foreground, _logger, out exitCode, _currentDirectory);
	}

	public int? Spawn(IReadOnlyList<string> argv)
	{
		if (argv.Count == 0)
			return null;

		if (_builtins.TryGet(argv[0], out var builtin))
		{
			ExecuteBuiltin(builtin, argv);
			return 0;
		}

		var job = ProcessRunner.StartBackground(argv[0], argv.Skip(1).ToArray(), _logger, _currentDirectory);
		if (job is null)
		{
			_lastExitCode = 127;
			return null;
		}

		job.Id = _nextJobId++;
		_lastBackgroundPid = job.Process.Id;
		_jobs.Add(job);
		Console.Out.WriteLine($"[{job.Id}] {job.Process.Id}");
		_logger.Information(Resources.LogJobStarted, job.Id, job.Command, job.Process.Id);
		JobStarted?.Invoke(this, new JobEventArgs(job));
		return job.Id;
	}

	public bool IsCommandAvailable(string name)
	{
		return _builtins.TryGet(name, out _)
			|| IsScriptFile(name)
			|| PathResolver.Resolve(name, _currentDirectory) is not null;
	}

	private bool IsScriptFile(string command)
	{
		var fullPath = Path.GetFullPath(command, _currentDirectory);
		if (!File.Exists(fullPath))
			return false;

		var extension = Path.GetExtension(fullPath);
		var isScriptExtension = ScriptExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
		var hasSeparator = command.Contains('/') || command.Contains('\\');

		if (hasSeparator)
			return !IsNativeExecutable(fullPath);
		return isScriptExtension;
	}

	private static bool IsNativeExecutable(string path)
	{
		var extension = Path.GetExtension(path);
		if (OperatingSystem.IsWindows())
		{
			return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".com", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
				|| extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
		}

		try
		{
			var mode = File.GetUnixFileMode(path);
			return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private int ExecuteScriptFile(string path, IReadOnlyList<string> argv)
	{
		var fullPath = Path.GetFullPath(path, _currentDirectory);

		var savedName = _scriptName;
		var savedArgs = _positionalArgs;
		_scriptName = path;
		_positionalArgs = argv.Skip(1).ToArray();
		try
		{
			_logger?.Debug(Resources.LogScriptFile, fullPath);
			return _lua.ExecuteFile(fullPath, File.ReadAllText(fullPath));
		}
		finally
		{
			_scriptName = savedName;
			_positionalArgs = savedArgs;
		}
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
				_lastExitCode = 2;
				return 2;
			}

			if (parseResult.HelpRequested)
			{
				HelpRenderer.PrintCommand(builtin.Spec);
				_lastExitCode = 0;
				return 0;
			}
		}

		var code = builtin.Execute(parseResult, this);
		_lastExitCode = code;
		return code;
	}

	private static IReadOnlyList<RedirectSpec>? TranslateLuaRedirects(IReadOnlyList<LuaRedirect>? redirects)
	{
		if (redirects is null || redirects.Count == 0)
			return null;
		return redirects.Select(ToSpec).ToArray();
	}

	private static RedirectSpec ToSpec(LuaRedirect redirect) => redirect.Mode switch
	{
		LuaRedirectMode.Input => new RedirectSpec(redirect.Fd, RedirectKind.Input, redirect.Target),
		LuaRedirectMode.Output => new RedirectSpec(redirect.Fd, RedirectKind.Output, redirect.Target),
		LuaRedirectMode.Append => new RedirectSpec(redirect.Fd, RedirectKind.Append, redirect.Target),
		LuaRedirectMode.DupOutput => new RedirectSpec(2, RedirectKind.DupOutput, redirect.Target),
		_ => throw new InvalidOperationException(redirect.Mode.ToString()),
	};

	// ---- IShellContext ----

	public int LastExitCode
	{
		get => _lastExitCode;
		set => _lastExitCode = value;
	}

	public string? PreviousDirectory { get; set; }

	public string CurrentDirectory
	{
		get => _currentDirectory;
		set => _currentDirectory = value;
	}

	public bool ExitRequested => _exitRequested;
	public int RequestedExitCode => _exitCode;
	public void RequestExit(int exitCode)
	{
		_exitRequested = true;
		_exitCode = exitCode;
	}

	public void PrintJobs() => ListJobs();

	public TextReader? PipelineInput => _pipelineInput;

	public string? GetVariable(string name) =>
		_lua.GetGlobalString(name) ?? Environment.GetEnvironmentVariable(name);

	public void SetVariable(string name, string value, bool exported)
	{
		_lua.SetGlobal(name, value);
		if (exported)
			Environment.SetEnvironmentVariable(name, value);
	}

	public void ExportVariable(string name)
	{
		var value = _lua.GetGlobalString(name) ?? Environment.GetEnvironmentVariable(name);
		if (value is not null)
			Environment.SetEnvironmentVariable(name, value);
	}

	public void UnsetVariable(string name)
	{
		_lua.UnsetGlobal(name);
		Environment.SetEnvironmentVariable(name, null);
	}

	public void ShiftArguments(int count)
	{
		var skip = Math.Min(count, _positionalArgs.Count);
		PositionalArgs = _positionalArgs.Skip(skip).ToArray();
	}

	public int RunScriptFile(string path)
	{
		var full = Path.GetFullPath(path, _currentDirectory);
		if (!File.Exists(full))
		{
			Console.Error.WriteLine(string.Format(Resources.ShellSourceFileNotFound, path));
			return 1;
		}
		return _lua.ExecuteFile(full, File.ReadAllText(full));
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

			Console.Out.WriteLine(string.Format(Resources.ShellJobDone, job.Id, job.ExitCode));
			if (output.Length > 0)
				Console.Out.Write(output);

			_logger.Information(Resources.LogJobCompleted, job.Id, job.ExitCode);
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
				Console.Out.WriteLine(string.Format(Resources.ShellJobRunning, job.Id, job.Command, job.Process.Id));
		}
	}
}

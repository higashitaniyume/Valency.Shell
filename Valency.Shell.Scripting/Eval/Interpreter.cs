using System.Diagnostics;
using Serilog;
using Valency.Shell.Scripting.Ast;
using Valency.Shell.Scripting.Expansion;
using Valency.Shell.Scripting.Expressions;
using Valency.Shell.Scripting.Lexing;
using Valency.Shell.Scripting.Parsing;

namespace Valency.Shell.Scripting.Eval;

public sealed class Interpreter
{
	private readonly IShellRuntime _runtime;
	private readonly ShellState _state;
	private readonly WordExpander _expander;
	private readonly ILogger? _logger;

	public Interpreter(IShellRuntime runtime, ShellState state, ILogger? logger = null)
	{
		_runtime = runtime;
		_state = state;
		_logger = logger?.ForContext("Src", "interp");
		_expander = new WordExpander(new StateVariableSource(state), Capture);
	}

	public ShellState State => _state;

	public int Execute(string text) => Execute(Parser.Parse(text, _logger));

	public int Execute(Script script)
	{
		try
		{
			ExecuteStatements(script.Statements);
			return _state.LastExitCode;
		}
		catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Exit)
		{
			_state.ExitRequested = true;
			_state.ExitCode = cf.Code;
			_state.LastExitCode = cf.Code;
			return cf.Code;
		}
	}

	private Value GetExpressionValue(string name)
	{
		return Value.FromString(_state.GetVariable(name) ?? string.Empty);
	}

	private void SetExpressionValue(string name, Value value)
	{
		_state.SetVariable(name, value.AsString(), exported: false);
		_logger?.Debug(Resources.LogVariableAssigned, name, value.AsString());
	}

	private string Capture(string text)
	{
		_logger?.Debug(Resources.LogCommandSubstitution, text);
		var prevOut = Console.Out;
		var prevErr = Console.Error;
		var writer = new StringWriter();
		try
		{
			Console.SetOut(writer);
			Console.SetError(writer);
			try
			{
				Execute(text);
			}
			catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Exit)
			{
			}
		}
		finally
		{
			Console.SetOut(prevOut);
			Console.SetError(prevErr);
		}
		return writer.ToString().TrimEnd('\r', '\n');
	}

	private void ExecuteStatements(IReadOnlyList<Statement> statements)
	{
		foreach (var statement in statements)
			ExecuteStatement(statement);
	}

	private void ExecuteStatement(Statement statement)
	{
		_logger?.Debug(Resources.LogStatementExecuted, statement.GetType().Name);

		switch (statement)
		{
			case BlockStatement block:
				ExecuteStatements(block.Statements);
				break;
			case IfStatement ifStatement:
				ExecuteIf(ifStatement);
				break;
			case WhileStatement whileStatement:
				ExecuteWhile(whileStatement);
				break;
			case ForStatement forStatement:
				ExecuteFor(forStatement);
				break;
			case FunctionDecl function:
				_state.Functions[function.Name] = function;
				_logger?.Debug(Resources.LogFunctionDefined, function.Name);
				break;
			case ReturnStatement returnStatement:
				throw new ControlFlowException(ControlFlowKind.Return, EvaluateReturn(returnStatement));
			case BreakStatement:
				throw new ControlFlowException(ControlFlowKind.Break, 0);
			case ContinueStatement:
				throw new ControlFlowException(ControlFlowKind.Continue, 0);
			case ExpressionStatement expression:
				EvaluateExpression(expression.Expression);
				break;
			case CommandStatement command:
				ExecuteCommandStatement(command);
				break;
		}
	}

	private int EvaluateReturn(ReturnStatement returnStatement)
	{
		if (returnStatement.Value is null)
			return _state.LastExitCode;
		var value = EvaluateExpression(returnStatement.Value);
		return (int)value.AsInt();
	}

	private Value EvaluateExpression(string expression)
	{
		return ExpressionEvaluator.Evaluate(
			expression,
			GetExpressionValue,
			SetExpressionValue,
			Capture,
			_logger);
	}

	private void ExecuteIf(IfStatement ifStatement)
	{
		if (EvaluateExpression(ifStatement.Condition).Truthy)
		{
			ExecuteStatements(ifStatement.Then.Statements);
			return;
		}

		foreach (var (condition, body) in ifStatement.ElseIfs)
		{
			if (EvaluateExpression(condition).Truthy)
			{
				ExecuteStatements(body.Statements);
				return;
			}
		}

		if (ifStatement.Else is not null)
			ExecuteStatements(ifStatement.Else.Statements);
	}

	private void ExecuteWhile(WhileStatement whileStatement)
	{
		while (true)
		{
			var truthy = EvaluateExpression(whileStatement.Condition).Truthy;
			var enter = whileStatement.Until ? !truthy : truthy;
			if (!enter)
				break;

			try
			{
				ExecuteStatements(whileStatement.Body.Statements);
			}
			catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Break)
			{
				break;
			}
			catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Continue)
			{
			}
		}
	}

	private void ExecuteFor(ForStatement forStatement)
	{
		if (forStatement.Init is not null)
			EvaluateExpression(forStatement.Init);

		while (true)
		{
			if (forStatement.Condition is not null && !EvaluateExpression(forStatement.Condition).Truthy)
				break;

			try
			{
				ExecuteStatements(forStatement.Body.Statements);
			}
			catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Break)
			{
				break;
			}
			catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Continue)
			{
			}

			if (forStatement.Post is not null)
				EvaluateExpression(forStatement.Post);
		}
	}

	private void ExecuteCommandStatement(CommandStatement statement)
	{
		if (statement.Background)
		{
			ExecuteBackground(statement.Command);
			return;
		}
		ExecuteAndOr(statement.Command);
	}

	private int ExecuteAndOr(AndOr andOr)
	{
		var code = ExecutePipeline(andOr.Pipeline);
		foreach (var (op, pipeline) in andOr.Rest)
		{
			var run = op == Connector.And ? code == 0 : code != 0;
			if (run)
				code = ExecutePipeline(pipeline);
		}
		_state.LastExitCode = code;
		return code;
	}

	private int ExecutePipeline(Pipeline pipeline)
	{
		int code;
		if (pipeline.Commands.Count == 1)
		{
			code = ExecuteCommand(pipeline.Commands[0]);
		}
		else
		{
			code = ExecuteMultiStagePipeline(pipeline.Commands);
		}

		if (pipeline.Negate)
			code = code == 0 ? 1 : 0;

		_state.LastExitCode = code;
		return code;
	}

	private int ExecuteMultiStagePipeline(IReadOnlyList<Command> commands)
	{
		var stages = new List<PipelineStage>();
		foreach (var command in commands)
		{
			if (command is not SimpleCommand sc)
				throw new SyntaxError(Resources.PipelineCompoundNotSupported, 0, 0);
			stages.Add(new PipelineStage(ExpandArgv(sc), ResolveRedirects(sc.Redirections)));
		}

		_logger?.Debug(Resources.LogPipelineExecuted, stages.Count);
		var commandText = string.Join(" | ", stages.Select(s => string.Join(' ', s.Argv)));
		var stopwatch = Stopwatch.StartNew();
		var code = _runtime.ExecutePipeline(stages);
		stopwatch.Stop();
		_logger?.Information(Resources.LogCommandExecuted, commandText, code, stopwatch.ElapsedMilliseconds);

		if (_state.ExitRequested)
			throw new ControlFlowException(ControlFlowKind.Exit, _state.ExitCode);
		return code;
	}

	private int ExecuteCommand(Command command)
	{
		return command is SimpleCommand sc ? ExecuteSimpleCommand(sc) : 0;
	}

	private int ExecuteSimpleCommand(SimpleCommand command)
	{
		if (command.Words.Count == 0)
			return 0;

		var argv = ExpandArgv(command);

		if (argv.Count > 0 && _state.Functions.TryGetValue(argv[0], out var function))
			return InvokeFunction(function, argv);

		var stopwatch = Stopwatch.StartNew();
		var code = _runtime.ExecuteSimpleCommand(argv, ResolveRedirects(command.Redirections));
		stopwatch.Stop();
		_logger?.Information(Resources.LogCommandExecuted, string.Join(' ', argv), code, stopwatch.ElapsedMilliseconds);

		if (_state.ExitRequested)
			throw new ControlFlowException(ControlFlowKind.Exit, _state.ExitCode);
		_state.LastExitCode = code;
		return code;
	}

	private IReadOnlyList<string> ExpandArgv(SimpleCommand command)
	{
		var argv = new List<string>();
		foreach (var word in command.Words)
		{
			var expanded = _expander.Expand(word);
			if (expanded.Glob)
			{
				var matches = GlobExpander.Expand(expanded.Text);
				_logger?.Debug(Resources.LogGlobExpanded, expanded.Text, string.Join(", ", matches));
				argv.AddRange(matches);
			}
			else
			{
				argv.Add(expanded.Text);
			}
		}
		_logger?.Debug(Resources.LogWordExpanded, string.Join(' ', command.Words.Select(w => w.Raw)), string.Join(", ", argv));
		return argv;
	}

	private IReadOnlyList<ResolvedRedirection> ResolveRedirects(IReadOnlyList<Redirection> redirects)
	{
		if (redirects.Count == 0)
			return [];
		var list = new List<ResolvedRedirection>(redirects.Count);
		foreach (var redirect in redirects)
		{
			var target = _expander.ExpandToString(redirect.Target);
			list.Add(new ResolvedRedirection(redirect.Fd, redirect.Kind, target));
			_logger?.Debug(Resources.LogRedirectResolved, redirect.Fd, redirect.Kind, target);
		}
		return list;
	}

	private int InvokeFunction(FunctionDecl function, IReadOnlyList<string> argv)
	{
		_logger?.Debug(Resources.LogFunctionInvoked, function.Name);
		var savedArgs = _state.PositionalArgs;
		var args = argv.Skip(1).ToArray();
		_state.PositionalArgs = args;

		for (var i = 0; i < function.Parameters.Count; i++)
		{
			var value = i < args.Length ? args[i] : string.Empty;
			_state.SetVariable(function.Parameters[i], value, exported: false);
		}

		try
		{
			try
			{
				ExecuteStatements(function.Body.Statements);
				return _state.LastExitCode;
			}
			catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Return)
			{
				return cf.Code;
			}
		}
		finally
		{
			_state.PositionalArgs = savedArgs;
		}
	}

	private void ExecuteBackground(AndOr andOr)
	{
		if (andOr.Rest.Count > 0 ||
			andOr.Pipeline.Commands.Count != 1 ||
			andOr.Pipeline.Commands[0] is not SimpleCommand sc)
		{
			throw new SyntaxError(Resources.BackgroundOnlySingleCommand, 0, 0);
		}

		_runtime.ExecuteBackground(ExpandArgv(sc));
	}
}

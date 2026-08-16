using Valency.Shell.Scripting.Arithmetic;
using Valency.Shell.Scripting.Ast;
using Valency.Shell.Scripting.Expansion;
using Valency.Shell.Scripting.Lexing;
using Valency.Shell.Scripting.Parsing;

namespace Valency.Shell.Scripting.Eval;

public sealed class Interpreter
{
    private readonly IShellRuntime _runtime;
    private readonly ShellState _state;
    private readonly WordExpander _expander;

    public Interpreter(IShellRuntime runtime, ShellState state)
    {
        _runtime = runtime;
        _state = state;
        _expander = new WordExpander(
            new StateVariableSource(state),
            Capture,
            ResolveNumber);
    }

    public ShellState State => _state;

    public int Execute(string text) => Execute(Parser.Parse(text));

    public int Execute(Script script)
    {
        try
        {
            var code = ExecuteList(script.Body);
            _state.LastExitCode = code;
            return code;
        }
        catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Exit)
        {
            _state.ExitRequested = true;
            _state.ExitCode = cf.Code;
            _state.LastExitCode = cf.Code;
            return cf.Code;
        }
    }

    public static bool RunNext(bool executed, Connector connector, int code)
    {
        if (!executed)
        {
            return connector switch
            {
                Connector.And => false,
                Connector.Or => true,
                _ => true,
            };
        }

        return connector switch
        {
            Connector.And => code == 0,
            Connector.Or => code != 0,
            _ => true,
        };
    }

    private long ResolveNumber(string name)
    {
        var value = _state.GetVariable(name) ?? "0";
        return long.TryParse(value, out var n) ? n : 0;
    }

    private string Capture(string text)
    {
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

    private int ExecuteList(CompoundList list)
    {
        var code = 0;
        var runNext = true;
        foreach (var entry in list.Entries)
        {
            if (runNext)
            {
                if (entry.Connector == Connector.Background)
                {
                    ExecuteBackground(entry.Command);
                    code = 0;
                }
                else
                {
                    code = ExecuteAndOr(entry.Command);
                }
            }

            var effective = entry.Connector == Connector.Background ? Connector.Semicolon : entry.Connector;
            runNext = RunNext(runNext, effective, code);
        }
        return code;
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
        return code;
    }

    private int ExecuteMultiStagePipeline(IReadOnlyList<Command> commands)
    {
        var stages = new List<PipelineStage>();
        foreach (var command in commands)
        {
            if (command is not SimpleCommand sc)
                throw new SyntaxError("管道中暂不支持复合命令", 0, 0);
            stages.Add(new PipelineStage(ExpandArgv(sc), ResolveRedirects(sc.Redirections)));
        }

        var code = _runtime.ExecutePipeline(stages);
        if (_state.ExitRequested)
            throw new ControlFlowException(ControlFlowKind.Exit, _state.ExitCode);
        _state.LastExitCode = code;
        return code;
    }

    private int ExecuteCommand(Command command)
    {
        switch (command)
        {
            case SimpleCommand sc:
                return ExecuteSimpleCommand(sc);
            case FunctionDef fd:
                _state.Functions[fd.Name] = fd;
                return 0;
            case IfCommand ic:
                return ExecuteIf(ic);
            case WhileCommand wc:
                return ExecuteWhile(wc);
            case ForInCommand fc:
                return ExecuteFor(fc);
            case CaseCommand cc:
                return ExecuteCase(cc);
            case BraceGroup bg:
                return ExecuteList(bg.Body);
            case Subshell ss:
                return ExecuteSubshell(ss);
            case ArithmeticCommand ac:
                return ExecuteArithmetic(ac);
            default:
                return 0;
        }
    }

    private int ExecuteSimpleCommand(SimpleCommand cmd)
    {
        foreach (var assignment in cmd.Assignments)
        {
            var value = _expander.ExpandToString(assignment.Value);
            if (assignment.Append)
            {
                var existing = _state.GetVariable(assignment.Name) ?? string.Empty;
                value = existing + value;
            }
            _state.SetVariable(assignment.Name, value, exported: false);
        }

        if (cmd.Words.Count == 0)
            return 0;

        var argv = ExpandArgv(cmd);

        if (argv.Count > 0 && _state.Functions.TryGetValue(argv[0], out var function))
            return InvokeFunction(function, argv);

        var code = _runtime.ExecuteSimpleCommand(argv, ResolveRedirects(cmd.Redirections));
        if (_state.ExitRequested)
            throw new ControlFlowException(ControlFlowKind.Exit, _state.ExitCode);
        _state.LastExitCode = code;
        return code;
    }

    private IReadOnlyList<string> ExpandArgv(SimpleCommand cmd)
    {
        var argv = new List<string>();
        foreach (var word in cmd.Words)
        {
            var expanded = _expander.Expand(word);
            if (expanded.Glob)
            {
                foreach (var match in GlobExpander.Expand(expanded.Text))
                    argv.Add(match);
            }
            else
            {
                argv.Add(expanded.Text);
            }
        }
        return argv;
    }

    private IReadOnlyList<ResolvedRedirection> ResolveRedirects(IReadOnlyList<Redirection> redirects)
    {
        if (redirects.Count == 0)
            return [];
        var list = new List<ResolvedRedirection>(redirects.Count);
        foreach (var redirect in redirects)
            list.Add(new ResolvedRedirection(redirect.Fd, redirect.Kind, _expander.ExpandToString(redirect.Target)));
        return list;
    }

    private int InvokeFunction(FunctionDef function, IReadOnlyList<string> argv)
    {
        var savedArgs = _state.PositionalArgs;
        _state.PositionalArgs = argv.Skip(1).ToArray();
        try
        {
            try
            {
                ExecuteCommand(function.Body);
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

    private int ExecuteIf(IfCommand ifCommand)
    {
        foreach (var branch in ifCommand.Branches)
        {
            if (ExecuteList(branch.Condition) == 0)
                return ExecuteList(branch.Body);
        }
        return ifCommand.Else is not null ? ExecuteList(ifCommand.Else) : 0;
    }

    private int ExecuteWhile(WhileCommand whileCommand)
    {
        while (true)
        {
            var cond = ExecuteList(whileCommand.Condition);
            var enter = whileCommand.Until ? cond != 0 : cond == 0;
            if (!enter)
                break;

            try
            {
                ExecuteList(whileCommand.Body);
            }
            catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Break)
            {
                break;
            }
            catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Continue)
            {
            }
        }
        return 0;
    }

    private int ExecuteFor(ForInCommand forCommand)
    {
        IReadOnlyList<string> items;
        if (forCommand.Items is null)
        {
            items = _state.PositionalArgs;
        }
        else
        {
            var list = new List<string>();
            foreach (var word in forCommand.Items)
            {
                var expanded = _expander.Expand(word);
                if (expanded.Glob)
                    list.AddRange(GlobExpander.Expand(expanded.Text));
                else
                    list.Add(expanded.Text);
            }
            items = list;
        }

        foreach (var item in items)
        {
            _state.SetVariable(forCommand.Variable, item, exported: false);
            try
            {
                ExecuteList(forCommand.Body);
            }
            catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Break)
            {
                break;
            }
            catch (ControlFlowException cf) when (cf.Kind == ControlFlowKind.Continue)
            {
            }
        }
        return 0;
    }

    private int ExecuteCase(CaseCommand caseCommand)
    {
        var value = _expander.ExpandToString(caseCommand.Word);
        foreach (var arm in caseCommand.Arms)
        {
            foreach (var pattern in arm.Patterns)
            {
                if (GlobExpander.Match(_expander.ExpandToString(pattern), value))
                    return ExecuteList(arm.Body);
            }
        }
        return 0;
    }

    private int ExecuteSubshell(Subshell subshell)
    {
        var savedDir = _state.CurrentDirectory;
        try
        {
            return ExecuteList(subshell.Body);
        }
        finally
        {
            _state.CurrentDirectory = savedDir;
        }
    }

    private int ExecuteArithmetic(ArithmeticCommand arithmeticCommand)
    {
        var result = ArithmeticEvaluator.Evaluate(
            arithmeticCommand.Expression,
            ResolveNumber,
            (name, value) => _state.SetVariable(name, value.ToString(), exported: false));
        return result != 0 ? 0 : 1;
    }

    private void ExecuteBackground(AndOr andOr)
    {
        if (andOr.Rest.Count > 0 ||
            andOr.Pipeline.Commands.Count != 1 ||
            andOr.Pipeline.Commands[0] is not SimpleCommand sc)
        {
            throw new SyntaxError("后台执行仅支持单个简单命令", 0, 0);
        }

        _runtime.ExecuteBackground(ExpandArgv(sc));
    }
}

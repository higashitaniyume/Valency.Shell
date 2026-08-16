using Valency.Shell.Core.Builtins;
using Valency.Shell.Scripting.Eval;

namespace Valency.Shell.Builtins;

public sealed class BreakCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Break,
        Summary = Resources.BreakSummary,
    };

    public int Execute(ParseResult args, IShellContext context)
        => throw new ControlFlowException(ControlFlowKind.Break, 0);
}

public sealed class ContinueCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Continue,
        Summary = Resources.ContinueSummary,
    };

    public int Execute(ParseResult args, IShellContext context)
        => throw new ControlFlowException(ControlFlowKind.Continue, 0);
}

public sealed class ReturnCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Return,
        Summary = Resources.ReturnSummary,
        Positionals = [Resources.ReturnPositional],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        var code = args.Positionals.Count > 0 && int.TryParse(args.Positionals[0], out var c)
            ? c
            : context.LastExitCode;
        throw new ControlFlowException(ControlFlowKind.Return, code);
    }
}

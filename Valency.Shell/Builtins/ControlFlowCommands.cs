using Valency.Shell.Core.Builtins;
using Valency.Shell.Scripting.Eval;

namespace Valency.Shell.Builtins;

public sealed class BreakCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Break,
        Summary = "跳出最近的循环。",
    };

    public int Execute(ParseResult args, IShellContext context)
        => throw new ControlFlowException(ControlFlowKind.Break, 0);
}

public sealed class ContinueCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Continue,
        Summary = "继续最近循环的下一次迭代。",
    };

    public int Execute(ParseResult args, IShellContext context)
        => throw new ControlFlowException(ControlFlowKind.Continue, 0);
}

public sealed class ReturnCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Return,
        Summary = "从函数返回。",
        Positionals = ["[n] 返回码，默认上一条命令的退出码"],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        var code = args.Positionals.Count > 0 && int.TryParse(args.Positionals[0], out var c)
            ? c
            : context.LastExitCode;
        throw new ControlFlowException(ControlFlowKind.Return, code);
    }
}

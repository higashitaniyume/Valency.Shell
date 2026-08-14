using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class ExitCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Exit,
        Summary = "退出 shell。",
        Positionals = ["[code] 退出码，默认使用上一条命令的退出码"],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        var code = args.Positionals.Count > 0 && int.TryParse(args.Positionals[0], out var c)
            ? c
            : context.LastExitCode;
        context.RequestExit(code);
        return code;
    }
}

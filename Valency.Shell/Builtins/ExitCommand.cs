using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class ExitCommand : IBuiltinCommand
{
    public string Name => BuiltinNames.Exit;

    public int Execute(IReadOnlyList<string> args, IShellContext context)
    {
        var code = args.Count > 1 && int.TryParse(args[1], out var c) ? c : context.LastExitCode;
        context.RequestExit(code);
        return code;
    }
}

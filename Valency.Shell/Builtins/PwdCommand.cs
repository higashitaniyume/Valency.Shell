using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class PwdCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Pwd,
        Summary = Resources.PwdSummary,
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        Console.Out.WriteLine(context.CurrentDirectory);
        return 0;
    }
}

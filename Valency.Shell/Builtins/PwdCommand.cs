using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class PwdCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Pwd,
        Summary = "打印当前工作目录。",
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        Console.Out.WriteLine(context.CurrentDirectory);
        return 0;
    }
}

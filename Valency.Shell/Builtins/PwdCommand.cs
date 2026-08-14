using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class PwdCommand : IBuiltinCommand
{
    public string Name => BuiltinNames.Pwd;

    public int Execute(IReadOnlyList<string> args, IShellContext context)
    {
        Console.Out.WriteLine(Environment.CurrentDirectory);
        return 0;
    }
}

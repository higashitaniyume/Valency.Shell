namespace Valency.Shell.Builtins;

public interface IBuiltinCommand
{
    string Name { get; }
    int Execute(IReadOnlyList<string> args, IShellContext context);
}

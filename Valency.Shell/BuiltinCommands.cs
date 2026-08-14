namespace Valency.Shell;

public static class BuiltinCommands
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "exit",
        "cd",
        "pwd",
    };

    public static bool IsBuiltin(string name) => Names.Contains(name);
}

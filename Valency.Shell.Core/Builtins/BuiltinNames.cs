namespace Valency.Shell.Core.Builtins;

public static class BuiltinNames
{
    public const string Exit = "exit";
    public const string Cd = "cd";
    public const string Pwd = "pwd";
    public const string Jobs = "jobs";
    public const string Logs = "logs";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Exit, Cd, Pwd, Jobs, Logs };

    public static bool IsBuiltin(string name) => All.Contains(name);
}

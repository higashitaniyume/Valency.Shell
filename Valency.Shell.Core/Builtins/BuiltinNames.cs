namespace Valency.Shell.Core.Builtins;

public static class BuiltinNames
{
    public const string Exit = "exit";
    public const string Cd = "cd";
    public const string Pwd = "pwd";
    public const string Jobs = "jobs";
    public const string Logs = "logs";
    public const string Prompt = "prompt";
    public const string Grep = "grep";
    public const string Help = "help";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Exit, Cd, Pwd, Jobs, Logs, Prompt, Grep, Help };

    public static bool IsBuiltin(string name) => All.Contains(name);
}

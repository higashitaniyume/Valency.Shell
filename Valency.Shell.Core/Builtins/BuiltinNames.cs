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
	public const string Echo = "echo";
	public const string Test = "test";
	public const string Bracket = "[";
	public const string True = "true";
	public const string False = "false";
	public const string Colon = ":";
	public const string Export = "export";
	public const string Unset = "unset";
	public const string Read = "read";
	public const string Source = "source";
	public const string Dot = ".";
	public const string Shift = "shift";

	public static readonly IReadOnlySet<string> All =
		new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			Exit, Cd, Pwd, Jobs, Logs, Prompt, Grep, Help,
			Echo, Test, Bracket, True, False, Colon, Export, Unset,
			Read, Source, Dot, Shift,
		};

	public static bool IsBuiltin(string name) => All.Contains(name);
}

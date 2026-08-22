namespace Valency.Shell.Builtins;

public readonly record struct OptionSpec(
	string LongName,
	char? ShortName = null,
	string? Description = null,
	bool IsFlag = false,
	string? ValueName = null);

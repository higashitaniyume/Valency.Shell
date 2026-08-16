namespace Valency.Shell.Builtins;

public sealed class CommandSpec
{
    public required string Name { get; init; }
    public required string Summary { get; init; }
    public IReadOnlyList<OptionSpec> Options { get; init; } = [];
    public IReadOnlyList<string> Positionals { get; init; } = [];
    public bool RawArgs { get; init; }

    public OptionSpec? FindOption(string token)
    {
        foreach (var option in Options)
        {
            if (string.Equals(option.LongName, token, StringComparison.OrdinalIgnoreCase))
                return option;
            if (option.ShortName is { } shortName && shortName.ToString() == token)
                return option;
        }
        return null;
    }
}

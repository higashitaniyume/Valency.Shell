namespace Valency.Shell.Builtins;

public sealed class ParseResult
{
	private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

	public List<string> Positionals { get; } = [];
	public bool HelpRequested { get; internal set; }

	public bool Has(string longName) => _options.ContainsKey(longName);

	public string? Get(string longName) =>
		_options.TryGetValue(longName, out var value) ? value : null;

	public int? GetInt(string longName) =>
		int.TryParse(Get(longName), out var number) ? number : null;

	internal void Set(string longName, string? value) => _options[longName] = value;
}

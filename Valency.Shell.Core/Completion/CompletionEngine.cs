namespace Valency.Shell.Core.Completion;

public sealed class CompletionEngine : ICompleter
{
	private readonly IReadOnlyList<string> _commands;

	public CompletionEngine(IEnumerable<string> builtinNames)
	{
		_commands = builtinNames
			.Concat(EnumeratePathCommands())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public CompletionResult? Complete(string line, int cursor)
	{
		var start = TokenStart(line, cursor);
		var token = line[start..cursor];
		var isCommand = IsCommandPosition(line, start) && token.IndexOfAny(['\\', '/']) < 0;
		var candidates = isCommand ? CompleteCommand(token) : CompletePath(token);

		if (candidates.Count == 0)
			return null;

		return new CompletionResult(start, candidates, isCommand);
	}

	private static StringComparison Comparison =>
		OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

	private static int TokenStart(string line, int cursor)
	{
		var start = cursor;
		while (start > 0 && !char.IsWhiteSpace(line[start - 1]) && line[start - 1] is not (';' or '|' or '&'))
			start--;
		return start;
	}

	private static bool IsCommandPosition(string line, int start)
	{
		var i = start - 1;
		while (i >= 0 && char.IsWhiteSpace(line[i]))
			i--;
		if (i < 0)
			return true;
		return line[i] is ';' or '|' or '&';
	}

	private List<string> CompleteCommand(string token)
	{
		var comparison = Comparison;
		return _commands.Where(name => name.StartsWith(token, comparison)).ToList();
	}

	private static List<string> CompletePath(string token)
	{
		var comparison = Comparison;

		var sepIndex = Math.Max(token.LastIndexOf('\\'), token.LastIndexOf('/'));
		string dirPart;
		string prefix;
		char separator;
		if (sepIndex >= 0)
		{
			dirPart = token[..sepIndex];
			prefix = token[(sepIndex + 1)..];
			separator = token[sepIndex];
		}
		else
		{
			dirPart = string.Empty;
			prefix = token;
			separator = Path.DirectorySeparatorChar;
		}

		var directory = dirPart.Length > 0 ? dirPart : ".";
		if (!Directory.Exists(directory))
			return [];

		var result = new List<string>();
		foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
		{
			var name = Path.GetFileName(entry);
			if (!name.StartsWith(prefix, comparison))
				continue;

			var isDirectory = Directory.Exists(entry);
			var value = (dirPart.Length > 0 ? dirPart + separator : "") + name + (isDirectory ? separator : "");
			result.Add(value);
		}

		result.Sort(StringComparer.OrdinalIgnoreCase);
		return result;
	}

	private static IEnumerable<string> EnumeratePathCommands()
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
		{
			if (!Directory.Exists(directory))
				continue;

			foreach (var file in Directory.EnumerateFiles(directory))
			{
				if (!IsExecutable(file))
					continue;
				result.Add(DisplayName(Path.GetFileName(file)));
			}
		}

		return result;
	}

	private static string DisplayName(string fileName)
	{
		if (!OperatingSystem.IsWindows())
			return fileName;

		var extension = Path.GetExtension(fileName);
		return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
			   extension.Equals(".com", StringComparison.OrdinalIgnoreCase)
			? Path.GetFileNameWithoutExtension(fileName)
			: fileName;
	}

	private static bool IsExecutable(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			var extension = Path.GetExtension(path);
			var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
			return pathExt
				.Split(';', StringSplitOptions.RemoveEmptyEntries)
				.Any(ext => extension.Equals(ext.Trim(), StringComparison.OrdinalIgnoreCase));
		}

		try
		{
			var mode = File.GetUnixFileMode(path);
			return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
		}
		catch (Exception)
		{
			return false;
		}
	}
}

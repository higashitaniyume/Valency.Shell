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
		// 标识符 token：调用位置（行首或 ( , = { 之后）→ 命令/函数名补全
		var idStart = cursor;
		while (idStart > 0 && IsIdentifierChar(line[idStart - 1]))
			idStart--;

		if (cursor > idStart && IsCallPosition(line, idStart))
		{
			var candidates = CompleteCommand(line[idStart..cursor]);
			if (candidates.Count == 0)
				return null;
			return new CompletionResult(idStart, candidates, IsCommand: true);
		}

		// 路径 token：包含分隔符的片段（含字符串参数里的路径）→ 路径补全
		var pathStart = cursor;
		while (pathStart > 0 && !char.IsWhiteSpace(line[pathStart - 1]) &&
		       line[pathStart - 1] is not ('(' or ',' or '=' or '{'))
			pathStart--;

		if (pathStart < idStart)
		{
			var candidates = CompletePath(line[pathStart..cursor]);
			if (candidates.Count == 0)
				return null;
			return new CompletionResult(pathStart, candidates, IsCommand: false);
		}

		// 空 token 处于调用位置 → 列出全部命令
		if (cursor == idStart && IsCallPosition(line, cursor))
		{
			var candidates = CompleteCommand(string.Empty);
			if (candidates.Count == 0)
				return null;
			return new CompletionResult(cursor, candidates, IsCommand: true);
		}

		return null;
	}

	private static StringComparison Comparison =>
		OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

	private static bool IsIdentifierChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_';

	private static bool IsCallPosition(string line, int start)
	{
		var i = start - 1;
		while (i >= 0 && char.IsWhiteSpace(line[i]))
			i--;
		return i < 0 || line[i] is '(' or ',' or '=' or '{';
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

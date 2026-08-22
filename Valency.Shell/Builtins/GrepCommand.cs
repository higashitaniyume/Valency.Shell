using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class GrepCommand : IBuiltinCommand
{
	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Grep,
		Summary = Resources.GrepSummary,
		Positionals =
		[
			Resources.GrepPositionalPattern,
			Resources.GrepPositionalFile,
		],
		Options =
		[
			new("ignore-case", 'i', Resources.GrepIgnoreCase, true),
			new("invert-match", 'v', Resources.GrepInvertMatch, true),
			new("line-number", 'n', Resources.GrepLineNumber, true),
			new("count", 'c', Resources.GrepCount, true),
		],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		if (args.Positionals.Count == 0)
		{
			Console.Error.WriteLine(Resources.GrepMissingPattern);
			return 2;
		}

		var pattern = args.Positionals[0];
		var ignoreCase = args.Has("ignore-case");
		var invert = args.Has("invert-match");
		var lineNumber = args.Has("line-number");
		var countOnly = args.Has("count");
		var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		IEnumerable<string> lines;
		if (args.Positionals.Count > 1)
		{
			var collected = new List<string>();
			foreach (var file in args.Positionals.Skip(1))
			{
				if (!File.Exists(file))
				{
					Console.Error.WriteLine(string.Format(Resources.GrepFileNotFound, file));
					return 2;
				}
				collected.AddRange(File.ReadLines(file));
			}
			lines = collected;
		}
		else
		{
			lines = ReadAllLines(context.PipelineInput ?? Console.In);
		}

		var matched = 0;
		var lineNo = 0;
		foreach (var line in lines)
		{
			lineNo++;
			var isMatch = line.Contains(pattern, comparison);
			if (isMatch == invert)
				continue;

			matched++;
			if (countOnly)
				continue;

			if (lineNumber)
				Console.Out.WriteLine($"{lineNo}:{line}");
			else
				Console.Out.WriteLine(line);
		}

		if (countOnly)
			Console.Out.WriteLine(matched);

		return matched > 0 ? 0 : 1;
	}

	private static IEnumerable<string> ReadAllLines(TextReader reader)
	{
		while (reader.ReadLine() is { } line)
			yield return line;
	}
}

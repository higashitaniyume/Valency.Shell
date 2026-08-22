using System.Text;

namespace Valency.Shell.Builtins;

public static class HelpRenderer
{
	public static void PrintCommand(CommandSpec spec)
	{
		Console.Out.WriteLine($"{spec.Name} - {spec.Summary}");
		Console.Out.WriteLine();
		Console.Out.WriteLine(string.Format(Resources.HelpUsage, spec.Name, BuildUsage(spec)));

		if (spec.Positionals.Count > 0)
		{
			Console.Out.WriteLine();
			Console.Out.WriteLine(Resources.HelpPositionals);
			foreach (var positional in spec.Positionals)
				Console.Out.WriteLine($"  {positional}");
		}

		if (spec.Options.Count > 0)
		{
			Console.Out.WriteLine();
			Console.Out.WriteLine(Resources.HelpOptions);
			foreach (var option in spec.Options)
				Console.Out.WriteLine($"  {FormatOption(option),-24} {option.Description}");
		}
	}

	private static string BuildUsage(CommandSpec spec)
	{
		var parts = new List<string>();
		foreach (var option in spec.Options)
			parts.Add($"[{FormatOption(option)}]");
		foreach (var positional in spec.Positionals)
			parts.Add(positional);
		return string.Join(' ', parts);
	}

	private static string FormatOption(OptionSpec option)
	{
		var sb = new StringBuilder();
		if (option.ShortName is { } shortName)
			sb.Append($"-{shortName}, ");
		else
			sb.Append("    ");
		sb.Append($"--{option.LongName}");
		if (!option.IsFlag)
			sb.Append($" <{option.ValueName ?? "value"}>");
		return sb.ToString();
	}
}

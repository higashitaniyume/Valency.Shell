using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class ExportCommand : IBuiltinCommand
{
	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Export,
		Summary = Resources.ExportSummary,
		Positionals = [Resources.ExportPositional],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		foreach (var arg in args.Positionals)
		{
			var eq = arg.IndexOf('=');
			if (eq > 0)
			{
				var name = arg[..eq];
				context.SetVariable(name, arg[(eq + 1)..], exported: true);
			}
			else
			{
				context.ExportVariable(arg);
			}
		}
		return 0;
	}
}

public sealed class UnsetCommand : IBuiltinCommand
{
	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Unset,
		Summary = Resources.UnsetSummary,
		Positionals = [Resources.UnsetPositional],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		foreach (var name in args.Positionals)
			context.UnsetVariable(name);
		return 0;
	}
}

public sealed class ReadCommand : IBuiltinCommand
{
	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Read,
		Summary = Resources.ReadSummary,
		Positionals = [Resources.ReadPositional],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		if (args.Positionals.Count == 0)
		{
			Console.Error.WriteLine(Resources.ReadNeedVariable);
			return 2;
		}

		var reader = context.PipelineInput ?? Console.In;
		var line = reader.ReadLine();
		if (line is null)
			return 1;

		if (args.Positionals.Count == 1)
		{
			context.SetVariable(args.Positionals[0], line, exported: false);
			return 0;
		}

		var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		for (var i = 0; i < args.Positionals.Count; i++)
		{
			var value = i < parts.Length ? parts[i] : string.Empty;
			if (i == args.Positionals.Count - 1 && parts.Length > args.Positionals.Count)
				value = string.Join(" ", parts[(args.Positionals.Count - 1)..]);
			context.SetVariable(args.Positionals[i], value, exported: false);
		}
		return 0;
	}
}

public sealed class ShiftCommand : IBuiltinCommand
{
	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Shift,
		Summary = Resources.ShiftSummary,
		Positionals = [Resources.ShiftPositional],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		var count = 1;
		if (args.Positionals.Count > 0)
		{
			if (!int.TryParse(args.Positionals[0], out count))
			{
				Console.Error.WriteLine(Resources.ShiftInvalidCount);
				return 2;
			}
		}
		context.ShiftArguments(count);
		return 0;
	}
}

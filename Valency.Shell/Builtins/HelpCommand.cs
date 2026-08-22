using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class HelpCommand : IBuiltinCommand
{
	private BuiltinRegistry? _registry;

	public BuiltinRegistry? Registry
	{
		get => _registry;
		set => _registry = value;
	}

	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Help,
		Summary = Resources.HelpSummary,
		Positionals = [Resources.HelpPositional],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		if (_registry is null)
			return 1;

		if (args.Positionals.Count == 0)
		{
			Console.Out.WriteLine(Resources.HelpListTitle);
			foreach (var command in _registry.Commands)
				Console.Out.WriteLine($"  {command.Spec.Name,-12} {command.Spec.Summary}");
			return 0;
		}

		if (_registry.TryGet(args.Positionals[0], out var found))
		{
			HelpRenderer.PrintCommand(found.Spec);
			return 0;
		}

		Console.Error.WriteLine(string.Format(Resources.HelpUnknownCommand, args.Positionals[0]));
		return 1;
	}
}

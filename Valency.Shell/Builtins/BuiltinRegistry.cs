namespace Valency.Shell.Builtins;

public sealed class BuiltinRegistry
{
	private readonly Dictionary<string, IBuiltinCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

	public BuiltinRegistry(params IBuiltinCommand[] commands)
	{
		foreach (var command in commands)
			_commands[command.Spec.Name] = command;
	}

	public bool TryGet(string name, out IBuiltinCommand command)
		=> _commands.TryGetValue(name, out command!);

	public IReadOnlyList<IBuiltinCommand> Commands =>
		_commands.Values.OrderBy(c => c.Spec.Name, StringComparer.OrdinalIgnoreCase).ToArray();
}

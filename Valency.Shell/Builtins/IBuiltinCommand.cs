namespace Valency.Shell.Builtins;

public interface IBuiltinCommand
{
	CommandSpec Spec { get; }
	int Execute(ParseResult args, IShellContext context);
}

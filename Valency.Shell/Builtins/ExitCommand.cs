using Valency.Shell.Core.Builtins;
using Valency.Shell.Scripting.Eval;

namespace Valency.Shell.Builtins;

public sealed class ExitCommand : IBuiltinCommand
{
	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Exit,
		Summary = Resources.ExitSummary,
		Positionals = [Resources.ExitPositional],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		var code = args.Positionals.Count > 0 && int.TryParse(args.Positionals[0], out var c)
			? c
			: context.LastExitCode;
		throw new ControlFlowException(ControlFlowKind.Exit, code);
	}
}

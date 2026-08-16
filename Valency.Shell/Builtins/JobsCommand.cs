using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class JobsCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Jobs,
        Summary = Resources.JobsSummary,
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        context.PrintJobs();
        return 0;
    }
}

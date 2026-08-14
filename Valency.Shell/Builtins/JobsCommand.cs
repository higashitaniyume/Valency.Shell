using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class JobsCommand : IBuiltinCommand
{
    public string Name => BuiltinNames.Jobs;

    public int Execute(IReadOnlyList<string> args, IShellContext context)
    {
        context.PrintJobs();
        return 0;
    }
}

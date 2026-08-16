using Valency.Shell.Scripting.Ast;

namespace Valency.Shell.Scripting.Eval;

public readonly record struct ResolvedRedirection(int Fd, RedirectionKind Kind, string Target);

public readonly record struct PipelineStage(IReadOnlyList<string> Argv, IReadOnlyList<ResolvedRedirection> Redirects);

public interface IShellRuntime
{
    int ExecuteSimpleCommand(IReadOnlyList<string> argv, IReadOnlyList<ResolvedRedirection> redirects);

    int ExecutePipeline(IReadOnlyList<PipelineStage> stages);

    int ExecuteBackground(IReadOnlyList<string> argv);
}

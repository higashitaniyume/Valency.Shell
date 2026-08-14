namespace Valency.Shell.Core.Completion;

public readonly record struct CompletionResult(int Start, IReadOnlyList<string> Candidates, bool IsCommand);

public interface ICompleter
{
    CompletionResult? Complete(string line, int cursor);
}

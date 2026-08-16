namespace Valency.Shell.Scripting.Ast;

public abstract record Command : Node;

public sealed record SimpleCommand(
    IReadOnlyList<Redirection> Redirections,
    IReadOnlyList<Word> Words) : Command
{
    public string? CommandName => Words.Count > 0 ? Words[0].Raw : null;

    public static readonly SimpleCommand Empty = new([], []);
}

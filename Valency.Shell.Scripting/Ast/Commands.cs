namespace Valency.Shell.Scripting.Ast;

public abstract record Command : Node;

public sealed record SimpleCommand(
    IReadOnlyList<Assignment> Assignments,
    IReadOnlyList<Redirection> Redirections,
    IReadOnlyList<Word> Words) : Command
{
    public string? CommandName =>
        Words.Count > 0 ? Words[0].Raw : null;

    public static readonly SimpleCommand Empty = new([], [], []);
}

public sealed record FunctionDef(string Name, Command Body) : Command;

public sealed record Branch(CompoundList Condition, CompoundList Body);

public sealed record IfCommand(IReadOnlyList<Branch> Branches, CompoundList? Else) : Command;

public sealed record WhileCommand(CompoundList Condition, CompoundList Body, bool Until) : Command;

public sealed record ForInCommand(string Variable, IReadOnlyList<Word>? Items, CompoundList Body) : Command;

public sealed record CaseArm(IReadOnlyList<Word> Patterns, CompoundList Body);

public sealed record CaseCommand(Word Word, IReadOnlyList<CaseArm> Arms) : Command;

public sealed record BraceGroup(CompoundList Body) : Command;

public sealed record Subshell(CompoundList Body) : Command;

public sealed record ArithmeticCommand(string Expression) : Command;

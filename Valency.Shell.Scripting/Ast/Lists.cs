namespace Valency.Shell.Scripting.Ast;

public sealed record Script(IReadOnlyList<Statement> Statements) : Node;

public sealed record AndOr(Pipeline Pipeline, IReadOnlyList<(Connector Op, Pipeline Pipeline)> Rest) : Node
{
    public static AndOr Single(Pipeline pipeline) => new(pipeline, []);
}

public sealed record Pipeline(bool Negate, IReadOnlyList<Command> Commands) : Node;

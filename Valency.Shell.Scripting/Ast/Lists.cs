namespace Valency.Shell.Scripting.Ast;

public sealed record Script(CompoundList Body) : Node;

public sealed record Entry(AndOr Command, Connector Connector);

public sealed record CompoundList(IReadOnlyList<Entry> Entries) : Node
{
    public static readonly CompoundList Empty = new([]);
}

public sealed record AndOr(Pipeline Pipeline, IReadOnlyList<(Connector Op, Pipeline Pipeline)> Rest) : Node
{
    public static AndOr Single(Pipeline pipeline) => new(pipeline, []);
}

public sealed record Pipeline(bool Negate, IReadOnlyList<Command> Commands) : Node;

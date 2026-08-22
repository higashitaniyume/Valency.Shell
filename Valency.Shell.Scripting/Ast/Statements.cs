namespace Valency.Shell.Scripting.Ast;

public abstract record Statement : Node;

public sealed record BlockStatement(IReadOnlyList<Statement> Statements) : Statement;

public sealed record IfStatement(
	string Condition,
	BlockStatement Then,
	IReadOnlyList<(string Condition, BlockStatement Body)> ElseIfs,
	BlockStatement? Else) : Statement;

public sealed record WhileStatement(string Condition, BlockStatement Body, bool Until) : Statement;

public sealed record ForStatement(string? Init, string? Condition, string? Post, BlockStatement Body) : Statement;

public sealed record FunctionDecl(string Name, IReadOnlyList<string> Parameters, BlockStatement Body) : Statement;

public sealed record ReturnStatement(string? Value) : Statement;

public sealed record BreakStatement : Statement;

public sealed record ContinueStatement : Statement;

public sealed record ExpressionStatement(string Expression) : Statement;

public sealed record CommandStatement(AndOr Command, bool Background = false) : Statement;

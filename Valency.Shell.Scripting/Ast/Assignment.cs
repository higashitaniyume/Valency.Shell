namespace Valency.Shell.Scripting.Ast;

public sealed record Assignment(string Name, bool Append, Word Value);

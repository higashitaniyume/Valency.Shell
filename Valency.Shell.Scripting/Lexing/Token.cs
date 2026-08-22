using Valency.Shell.Scripting.Ast;

namespace Valency.Shell.Scripting.Lexing;

public sealed record Token(TokenType Type, string Text, int Line, int Column, Word? Word = null)
{
	public override string ToString() => $"{Type}({Text})";
}

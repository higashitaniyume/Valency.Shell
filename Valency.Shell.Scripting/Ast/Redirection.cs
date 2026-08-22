namespace Valency.Shell.Scripting.Ast;

public enum RedirectionKind
{
	Input,          // <
	Output,         // >
	Append,         // >>
	DupInput,       // <&
	DupOutput,      // >&
	DupOutputInput, // <>
	Heredoc,        // <<
	HeredocDash,    // <<-
	AndOutput,      // &>
	AndAppend,      // &>>
}

public sealed record Redirection(int Fd, RedirectionKind Kind, Word Target)
{
	public bool RedirectsError => Fd == 2 || Kind is RedirectionKind.AndOutput or RedirectionKind.AndAppend;
}

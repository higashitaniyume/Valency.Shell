namespace Valency.Shell.Scripting.Ast;

public abstract record Node;

public enum Connector
{
    None,
    Semicolon,
    And,
    Or,
    Background,
    Newline,
}

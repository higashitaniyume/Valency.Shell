namespace Valency.Shell.Scripting.Lexing;

public enum TokenType
{
    Word,
    Expression,
    AndIf,
    OrIf,
    Pipe,
    Semi,
    Background,
    Bang,
    LParen,
    RParen,
    LBrace,
    RBrace,
    RedirectIn,
    RedirectOut,
    Append,
    DLess,
    DLessDash,
    LessAnd,
    GreatAnd,
    AndGreat,
    AndGreatAnd,
    LessGreat,
    Newline,
    EndOfFile,
}

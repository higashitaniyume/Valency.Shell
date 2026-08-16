namespace Valency.Shell.Scripting.Lexing;

public enum TokenType
{
    Word,
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
    ArithCommand,
    Newline,
    EndOfFile,
}

namespace Valency.Shell.Scripting.Ast;

public abstract record WordPart;

public sealed record LiteralPart(string Text, bool Quoted = false) : WordPart;

public sealed record SingleQuotedPart(string Text) : WordPart;

public sealed record CommandSubPart(string Command) : WordPart;

public sealed record ArithSubPart(string Expression) : WordPart;

public sealed record Word(IReadOnlyList<WordPart> Parts)
{
    public string Raw => string.Concat(Parts.Select(p => p switch
    {
        LiteralPart l => l.Quoted ? "\"" + l.Text + "\"" : l.Text,
        SingleQuotedPart s => "'" + s.Text + "'",
        CommandSubPart c => "$(" + c.Command + ")",
        ArithSubPart a => "$((" + a.Expression + "))",
        _ => string.Empty,
    }));

    public static readonly Word Empty = new([]);
}

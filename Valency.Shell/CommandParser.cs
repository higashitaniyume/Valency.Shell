using System.Text;

namespace Valency.Shell;

public readonly record struct TokenSegment(string Text, bool Expand);

public sealed record Token(IReadOnlyList<TokenSegment> Segments)
{
    public string Text => string.Concat(Segments.Select(s => s.Text));
    public bool Expandable => Segments.Any(s => s.Expand);
}

public static class CommandParser
{
    public static List<string> Split(string input)
    {
        return SplitTokens(input).Select(t => t.Text).ToList();
    }

    public static List<Token> SplitTokens(string input)
    {
        var tokens = new List<Token>();
        var segments = new List<TokenSegment>();
        var current = new StringBuilder();
        var currentExpand = true;
        var hasToken = false;

        void FlushSegment()
        {
            if (current.Length > 0)
            {
                segments.Add(new TokenSegment(current.ToString(), currentExpand));
                current.Clear();
            }
        }

        void FlushToken()
        {
            FlushSegment();
            if (segments.Count > 0 || hasToken)
            {
                tokens.Add(new Token(segments.ToArray()));
                segments.Clear();
                hasToken = false;
            }
        }

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (ch == '\'')
            {
                FlushSegment();
                currentExpand = false;
                hasToken = true;
                i++;
                while (i < input.Length && input[i] != '\'')
                {
                    current.Append(input[i]);
                    i++;
                }
                if (i >= input.Length)
                    throw new FormatException("单引号未闭合");
                FlushSegment();
                currentExpand = true;
                continue;
            }

            if (ch == '"')
            {
                hasToken = true;
                i++;
                while (i < input.Length && input[i] != '"')
                {
                    if (input[i] == '\\' && i + 1 < input.Length && input[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    current.Append(input[i]);
                    i++;
                }
                if (i >= input.Length)
                    throw new FormatException("引号未闭合");
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                FlushToken();
                continue;
            }

            current.Append(ch);
        }

        FlushToken();
        return tokens;
    }
}

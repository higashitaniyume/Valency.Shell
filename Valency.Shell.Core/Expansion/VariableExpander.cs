using System.Text;
using Valency.Shell.Core.Syntax;

namespace Valency.Shell.Core.Expansion;

public sealed class VariableExpander
{
    private readonly IVariableSource _source;

    public VariableExpander(IVariableSource? source = null)
    {
        _source = source ?? new EnvironmentVariableSource();
    }

    public string Expand(Token token)
    {
        var text = token.Expandable
            ? string.Concat(token.Segments.Select(s => s.Expand ? ExpandText(s.Text) : s.Text))
            : token.Text;
        return ExpandTilde(text, token.Expandable);
    }

    public string ExpandText(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (ch == '\\' && i + 1 < text.Length && text[i + 1] == '$')
            {
                sb.Append('$');
                i++;
                continue;
            }

            if (ch != '$' || i + 1 >= text.Length)
            {
                sb.Append(ch);
                continue;
            }

            var next = text[i + 1];
            if (next == '{')
            {
                var end = text.IndexOf('}', i + 2);
                if (end < 0)
                {
                    sb.Append(ch);
                    continue;
                }
                sb.Append(Lookup(text[(i + 2)..end]));
                i = end;
            }
            else if (next == '?')
            {
                sb.Append(Lookup("?"));
                i++;
            }
            else if (MatchEnvPrefix(text, i + 1))
            {
                var nameStart = i + 5;
                var nameEnd = ScanName(text, nameStart);
                if (nameEnd == nameStart)
                {
                    sb.Append(ch);
                    continue;
                }
                sb.Append(Lookup(text[nameStart..nameEnd]));
                i = nameEnd - 1;
            }
            else if (char.IsLetter(next) || next == '_')
            {
                var nameEnd = ScanName(text, i + 1);
                sb.Append(Lookup(text[(i + 1)..nameEnd]));
                i = nameEnd - 1;
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    public static string ExpandTilde(string text, bool expandable)
    {
        if (!expandable || text.Length == 0 || text[0] != '~')
            return text;
        if (text.Length > 1 && text[1] is not ('/' or '\\'))
            return text;
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + text[1..];
    }

    private string Lookup(string name)
    {
        return _source.TryGet(name, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static bool MatchEnvPrefix(string text, int start)
    {
        return start + 4 <= text.Length &&
               string.Compare(text, start, "env:", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static int ScanName(string text, int start)
    {
        var i = start;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            i++;
        return i;
    }
}

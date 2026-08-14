namespace Valency.Shell;

public readonly record struct HighlightSpan(int Start, int Length, ConsoleColor Color);

public sealed class Highlighter
{
    private readonly Func<string, bool> _isCommandValid;
    private readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Highlighter(Func<string, bool>? isCommandValid = null)
    {
        _isCommandValid = isCommandValid ??
            (name => BuiltinCommands.IsBuiltin(name) || PathResolver.Resolve(name) is not null);
    }

    public IReadOnlyList<HighlightSpan> Highlight(string text)
    {
        if (text.Length == 0)
            return [];

        var colors = new ConsoleColor?[text.Length];
        PaintCommand(text, colors);
        PaintStrings(text, colors);
        PaintVariables(text, colors);
        return ToSpans(colors);
    }

    private void PaintCommand(string text, ConsoleColor?[] colors)
    {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        if (start >= text.Length)
            return;

        var end = ScanToken(text, start);
        var color = IsValid(text[start..end]) ? ConsoleColor.Blue : ConsoleColor.Red;
        Paint(colors, start, end, color);
    }

    private static void PaintStrings(string text, ConsoleColor?[] colors)
    {
        var inQuotes = false;
        var quoteChar = '"';
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (!inQuotes && ch is '"' or '\'')
            {
                inQuotes = true;
                quoteChar = ch;
                start = i;
            }
            else if (inQuotes && ch == quoteChar)
            {
                inQuotes = false;
                var color = quoteChar == '"' ? ConsoleColor.Yellow : ConsoleColor.DarkYellow;
                Paint(colors, start, i + 1, color);
            }
        }
        if (inQuotes)
            Paint(colors, start, text.Length, quoteChar == '"' ? ConsoleColor.Yellow : ConsoleColor.DarkYellow);
    }

    private static void PaintVariables(string text, ConsoleColor?[] colors)
    {
        var inSingleQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\'')
            {
                inSingleQuotes = !inSingleQuotes;
                continue;
            }
            if (inSingleQuotes || text[i] != '$' || i + 1 >= text.Length)
                continue;
            if (i > 0 && text[i - 1] == '\\')
                continue;

            var next = text[i + 1];
            int end;
            if (next == '{')
            {
                end = text.IndexOf('}', i + 2);
                if (end < 0) continue;
                end++;
            }
            else if (next == '?')
            {
                end = i + 2;
            }
            else
            {
                var nameStart = i + 1;
                if (nameStart + 4 <= text.Length &&
                    string.Compare(text, nameStart, "env:", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
                    nameStart += 4;
                end = nameStart;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                    end++;
                if (end == nameStart) continue;
            }

            Paint(colors, i, end, ConsoleColor.Magenta);
            i = end - 1;
        }
    }

    private bool IsValid(string command)
    {
        if (command.Length == 0)
            return true;
        if (!_cache.TryGetValue(command, out var valid))
        {
            valid = _isCommandValid(command);
            _cache[command] = valid;
        }
        return valid;
    }

    private static int ScanToken(string text, int start)
    {
        var i = start;
        var inQuotes = false;
        var quoteChar = '"';
        while (i < text.Length)
        {
            var ch = text[i];
            if (ch is '"' or '\'')
            {
                if (!inQuotes)
                {
                    inQuotes = true;
                    quoteChar = ch;
                }
                else if (ch == quoteChar)
                {
                    inQuotes = false;
                }
            }
            else if (!inQuotes && char.IsWhiteSpace(ch))
            {
                break;
            }
            i++;
        }
        return i;
    }

    private static void Paint(ConsoleColor?[] colors, int start, int end, ConsoleColor color)
    {
        for (var i = start; i < end; i++)
            colors[i] = color;
    }

    private static IReadOnlyList<HighlightSpan> ToSpans(ConsoleColor?[] colors)
    {
        var spans = new List<HighlightSpan>();
        var i = 0;
        while (i < colors.Length)
        {
            if (colors[i] is null)
            {
                i++;
                continue;
            }
            var color = colors[i]!.Value;
            var start = i;
            while (i < colors.Length && colors[i] == color)
                i++;
            spans.Add(new HighlightSpan(start, i - start, color));
        }
        return spans;
    }
}

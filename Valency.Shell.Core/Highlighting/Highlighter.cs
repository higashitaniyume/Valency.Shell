using Valency.Shell.Core.Builtins;
using Valency.Shell.Core.Resolution;

namespace Valency.Shell.Core.Highlighting;

/// <summary>
///     Paints a Lua line: keywords, strings (quoted and long), comments, and
///     call-position identifiers checked against known commands (blue/red).
/// </summary>
public sealed class Highlighter
{
	private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
	{
		"and", "break", "do", "else", "elseif", "end", "false", "for", "function",
		"goto", "if", "in", "local", "nil", "not", "or", "repeat", "return",
		"then", "true", "until", "while",
	};

	private readonly Func<string, bool> _isCommandValid;
	private readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

	public Highlighter(Func<string, bool>? isCommandValid = null)
	{
		_isCommandValid = isCommandValid ??
			(name => BuiltinNames.IsBuiltin(name) || PathResolver.Resolve(name) is not null);
	}

	public IReadOnlyList<HighlightSpan> Highlight(string text)
	{
		if (text.Length == 0)
			return [];

		var colors = new ConsoleColor?[text.Length];
		Paint(text, colors);
		return ToSpans(colors);
	}

	private void Paint(string text, ConsoleColor?[] colors)
	{
		var i = 0;
		while (i < text.Length)
		{
			var ch = text[i];

			if (ch == '-' && i + 1 < text.Length && text[i + 1] == '-')
			{
				Paint(colors, i, text.Length, ConsoleColor.DarkGreen);
				return;
			}

			if (ch is '"' or '\'')
			{
				var end = ScanString(text, i, ch);
				Paint(colors, i, end, ConsoleColor.Yellow);
				i = end;
				continue;
			}

			if (ch == '[' && i + 1 < text.Length && text[i + 1] == '[')
			{
				var close = text.IndexOf("]]", i + 2, StringComparison.Ordinal);
				var end = close < 0 ? text.Length : close + 2;
				Paint(colors, i, end, ConsoleColor.Yellow);
				i = end;
				continue;
			}

			if (char.IsLetter(ch) || ch == '_')
			{
				var end = i;
				while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
					end++;
				var word = text[i..end];
				if (Keywords.Contains(word))
				{
					Paint(colors, i, end, ConsoleColor.Cyan);
				}
				else if (IsCallPosition(text, end))
				{
					Paint(colors, i, end, IsValid(word) ? ConsoleColor.Blue : ConsoleColor.Red);
				}
				i = end;
				continue;
			}

			i++;
		}
	}

	private static bool IsCallPosition(string text, int end)
	{
		var j = end;
		while (j < text.Length && char.IsWhiteSpace(text[j]))
			j++;
		return j < text.Length && text[j] == '(';
	}

	private static int ScanString(string text, int start, char quote)
	{
		var i = start + 1;
		while (i < text.Length)
		{
			if (text[i] == '\\')
			{
				i += 2;
				continue;
			}
			if (text[i] == quote)
				return i + 1;
			i++;
		}
		return text.Length;
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

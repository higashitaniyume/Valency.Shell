using System.Text;
using System.Text.RegularExpressions;

namespace Valency.Shell.Scripting.Expansion;

public static class GlobExpander
{
    private static readonly char[] GlobChars = ['*', '?', '['];
    private static readonly char[] Separators = ['/', '\\'];

    public static IReadOnlyList<string> Expand(string pattern)
    {
        if (pattern.IndexOfAny(GlobChars) < 0)
            return [pattern];

        var results = new List<string>();
        ExpandInto([""], pattern, results);
        return results.Count > 0 ? results : [pattern];
    }

    public static bool HasGlob(string text) => text.IndexOfAny(GlobChars) >= 0;

    public static bool Match(string pattern, string text)
    {
        return GlobToRegex(pattern, anchored: true).IsMatch(text);
    }

    private static void ExpandInto(IReadOnlyList<string> bases, string pattern, List<string> results)
    {
        if (pattern.Length == 0)
        {
            results.AddRange(bases);
            return;
        }

        var sepIndex = pattern.IndexOfAny(Separators);
        string segment;
        string rest;
        char separator = '/';
        if (sepIndex < 0)
        {
            segment = pattern;
            rest = string.Empty;
        }
        else
        {
            segment = pattern[..sepIndex];
            rest = pattern[(sepIndex + 1)..];
            separator = pattern[sepIndex];
        }

        var isLeaf = rest.Length == 0;
        var segmentHasGlob = segment.IndexOfAny(GlobChars) >= 0;

        foreach (var baseDir in bases)
        {
            var directory = baseDir.Length == 0 ? "." : baseDir;

            if (segmentHasGlob)
            {
                if (!Directory.Exists(directory))
                    continue;
                var regex = GlobToRegex(segment, anchored: true);
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    var name = Path.GetFileName(entry);
                    if (!regex.IsMatch(name))
                        continue;
                    var full = baseDir.Length == 0 ? name : baseDir + separator + name;
                    if (isLeaf)
                        results.Add(full);
                    else
                        ExpandInto([full], rest, results);
                }
            }
            else
            {
                var full = baseDir.Length == 0 ? segment : baseDir + separator + segment;
                if (isLeaf)
                {
                    if (File.Exists(full) || Directory.Exists(full))
                        results.Add(full);
                }
                else
                {
                    ExpandInto([full], rest, results);
                }
            }
        }
    }

    private static Regex GlobToRegex(string pattern, bool anchored)
    {
        var sb = new StringBuilder();
        if (anchored)
            sb.Append('^');
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            switch (c)
            {
                case '*':
                    sb.Append(".*");
                    i++;
                    break;
                case '?':
                    sb.Append('.');
                    i++;
                    break;
                case '[':
                    var end = pattern.IndexOf(']', i + 1);
                    if (end > i + 1)
                    {
                        var cls = pattern[(i + 1)..end];
                        var negated = cls.StartsWith('!') || cls.StartsWith('^');
                        if (negated)
                            cls = cls[1..];
                        sb.Append('[');
                        if (negated)
                            sb.Append('^');
                        sb.Append(Regex.Escape(cls));
                        sb.Append(']');
                        i = end + 1;
                    }
                    else
                    {
                        sb.Append(Regex.Escape(c.ToString()));
                        i++;
                    }
                    break;
                default:
                    sb.Append(Regex.Escape(c.ToString()));
                    i++;
                    break;
            }
        }
        if (anchored)
            sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant);
    }
}

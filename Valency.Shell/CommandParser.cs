using System.Text;

namespace Valency.Shell;

public static class CommandParser
{
    public static List<string> Split(string input)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (inQuotes)
            {
                if (ch == '\\' && i + 1 < input.Length && input[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (hasToken || current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (inQuotes)
            throw new FormatException("引号未闭合");

        if (hasToken || current.Length > 0)
            result.Add(current.ToString());

        return result;
    }
}

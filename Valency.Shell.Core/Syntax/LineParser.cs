using System.Text;

namespace Valency.Shell.Core.Syntax;

public enum Connector
{
    None,
    Semicolon,
    And,
    Or,
    Pipe,
    Background,
}

public readonly record struct ParsedCommand(string RawText, Connector Connector);

public static class LineParser
{
    public static List<ParsedCommand> Parse(string line)
    {
        var result = new List<ParsedCommand>();
        var sb = new StringBuilder();
        var i = 0;

        void Flush(Connector connector)
        {
            var text = sb.ToString().Trim();
            if (text.Length > 0)
                result.Add(new ParsedCommand(text, connector));
            sb.Clear();
        }

        while (i < line.Length)
        {
            var c = line[i];

            if (c is '\'' or '"')
            {
                var quote = c;
                sb.Append(c);
                i++;
                while (i < line.Length && line[i] != quote)
                {
                    if (quote == '"' && line[i] == '\\' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append("\"");
                        i += 2;
                        continue;
                    }
                    sb.Append(line[i]);
                    i++;
                }
                if (i < line.Length)
                {
                    sb.Append(line[i]);
                    i++;
                }
                continue;
            }

            if (c == ';')
            {
                Flush(Connector.Semicolon);
                i++;
                continue;
            }

            if (c == '&' && i + 1 < line.Length && line[i + 1] == '&')
            {
                Flush(Connector.And);
                i += 2;
                continue;
            }

            if (c == '|' && i + 1 < line.Length && line[i + 1] == '|')
            {
                Flush(Connector.Or);
                i += 2;
                continue;
            }

            if (c == '|')
            {
                Flush(Connector.Pipe);
                i++;
                continue;
            }

            if (c == '&')
            {
                Flush(Connector.Background);
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        Flush(Connector.None);
        return result;
    }
}

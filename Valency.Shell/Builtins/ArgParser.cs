namespace Valency.Shell.Builtins;

public static class ArgParser
{
    public static ParseResult? Parse(IReadOnlyList<string> args, CommandSpec spec, out string? error)
    {
        var result = new ParseResult();
        error = null;
        var positionalOnly = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (!positionalOnly && arg.Length > 1 && arg[0] == '-')
            {
                var body = arg[1..];
                if (body.Length > 0 && body[0] == '-')
                    body = body[1..];

                if (body.Length == 0)
                {
                    positionalOnly = true;
                    continue;
                }

                string? inlineValue = null;
                var name = body;
                var separator = body.IndexOfAny(['=', ':']);
                if (separator >= 0)
                {
                    name = body[..separator];
                    inlineValue = body[(separator + 1)..];
                }

                if (name.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("h", StringComparison.OrdinalIgnoreCase))
                {
                    result.HelpRequested = true;
                    continue;
                }

                var option = spec.FindOption(name);
                if (option is null)
                {
                    error = string.Format(Resources.ArgParserUnknownOption, arg);
                    return null;
                }

                if (option.Value.IsFlag)
                {
                    result.Set(option.Value.LongName, "true");
                    continue;
                }

                if (inlineValue is not null)
                {
                    result.Set(option.Value.LongName, inlineValue);
                }
                else if (i + 1 < args.Count)
                {
                    result.Set(option.Value.LongName, args[++i]);
                }
                else
                {
                    error = string.Format(Resources.ArgParserMissingValue, arg);
                    return null;
                }
            }
            else
            {
                result.Positionals.Add(arg);
            }
        }

        return result;
    }
}

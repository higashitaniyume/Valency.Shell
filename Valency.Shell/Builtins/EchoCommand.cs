using System.Text;
using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class EchoCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Echo,
        Summary = Resources.EchoSummary,
        Positionals = [Resources.EchoPositional],
        Options =
        [
            new("no-newline", 'n', Resources.EchoNoNewline, true),
            new("enable-escapes", 'e', Resources.EchoEnableEscapes, true),
        ],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        var text = string.Join(" ", args.Positionals);
        if (args.Has("enable-escapes"))
            text = Unescape(text);

        Console.Out.Write(text);
        if (!args.Has("no-newline"))
            Console.Out.WriteLine();
        return 0;
    }

    private static string Unescape(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 >= text.Length)
            {
                sb.Append(text[i]);
                continue;
            }

            i++;
            sb.Append(text[i] switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '\\' => '\\',
                '"' => '"',
                '\'' => '\'',
                _ => text[i],
            });
        }
        return sb.ToString();
    }
}

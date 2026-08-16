using System.Text;
using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class EchoCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Echo,
        Summary = "输出一行文本。",
        Positionals = ["[text...] 要输出的内容"],
        Options =
        [
            new("no-newline", 'n', "不输出末尾换行", true),
            new("enable-escapes", 'e', "解释反斜杠转义", true),
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

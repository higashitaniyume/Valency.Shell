using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class GrepCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Grep,
        Summary = "筛选包含指定字符串的行（从标准输入或文件）。",
        Positionals =
        [
            "pattern 要匹配的字符串",
            "[file...] 要筛选的文件，缺省读标准输入",
        ],
        Options =
        [
            new("ignore-case", 'i', "忽略大小写", true),
            new("invert-match", 'v', "反向：只输出不匹配的行", true),
            new("line-number", 'n', "显示行号", true),
            new("count", 'c', "只输出匹配行数", true),
        ],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        if (args.Positionals.Count == 0)
        {
            Console.Error.WriteLine("grep: 缺少 pattern");
            return 2;
        }

        var pattern = args.Positionals[0];
        var ignoreCase = args.Has("ignore-case");
        var invert = args.Has("invert-match");
        var lineNumber = args.Has("line-number");
        var countOnly = args.Has("count");
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        IEnumerable<string> lines;
        if (args.Positionals.Count > 1)
        {
            var collected = new List<string>();
            foreach (var file in args.Positionals.Skip(1))
            {
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine($"grep: 文件不存在: {file}");
                    return 2;
                }
                collected.AddRange(File.ReadLines(file));
            }
            lines = collected;
        }
        else
        {
            lines = ReadAllLines(context.PipelineInput ?? Console.In);
        }

        var matched = 0;
        var lineNo = 0;
        foreach (var line in lines)
        {
            lineNo++;
            var isMatch = line.Contains(pattern, comparison);
            if (isMatch == invert)
                continue;

            matched++;
            if (countOnly)
                continue;

            if (lineNumber)
                Console.Out.WriteLine($"{lineNo}:{line}");
            else
                Console.Out.WriteLine(line);
        }

        if (countOnly)
            Console.Out.WriteLine(matched);

        return matched > 0 ? 0 : 1;
    }

    private static IEnumerable<string> ReadAllLines(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }
}

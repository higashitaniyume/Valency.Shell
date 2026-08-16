using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class CdCommand : IBuiltinCommand
{
    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Cd,
        Summary = "切换当前目录。无参数回到用户目录，'-' 回到上一个目录。",
        Positionals = ["[dir] 目标目录"],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        var path = args.Positionals.Count > 0 ? args.Positionals[0] : null;
        var target = path switch
        {
            null => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "-" => context.PreviousDirectory ?? context.CurrentDirectory,
            _ => path,
        };

        try
        {
            var full = Path.GetFullPath(target, context.CurrentDirectory);
            if (!Directory.Exists(full))
            {
                Console.Error.WriteLine($"cd: 路径不存在: {target}");
                return 1;
            }

            context.PreviousDirectory = context.CurrentDirectory;
            context.CurrentDirectory = full;
            Environment.CurrentDirectory = full;
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"cd: {ex.Message}");
            return 1;
        }
    }
}

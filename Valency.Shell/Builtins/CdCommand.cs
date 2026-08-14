using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class CdCommand : IBuiltinCommand
{
    public string Name => BuiltinNames.Cd;

    public int Execute(IReadOnlyList<string> args, IShellContext context)
    {
        var path = args.Count > 1 ? args[1] : null;
        var target = path switch
        {
            null => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "-" => context.PreviousDirectory ?? Environment.CurrentDirectory,
            _ => path,
        };

        try
        {
            var full = Path.GetFullPath(target, Environment.CurrentDirectory);
            if (!Directory.Exists(full))
            {
                Console.Error.WriteLine($"cd: 路径不存在: {target}");
                return 1;
            }

            context.PreviousDirectory = Environment.CurrentDirectory;
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

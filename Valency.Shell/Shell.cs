namespace Valency.Shell;

public sealed class Shell
{
    private readonly LineEditor _editor = new();
    private string? _previousDirectory;
    public int LastExitCode { get; private set; }

    public int Run()
    {
        while (true)
        {
            var result = _editor.ReadLine($"valency {Environment.CurrentDirectory}> ");
            if (result.Kind == LineResultKind.Exit)
                return LastExitCode;
            if (result.Kind == LineResultKind.Cancelled)
            {
                LastExitCode = 1;
                continue;
            }

            var args = CommandParser.Split(result.Text);
            if (args.Count == 0)
                continue;

            if (TryRunBuiltin(args, out var exitCode))
            {
                if (exitCode < 0)
                    return -exitCode - 1;
                LastExitCode = exitCode;
                continue;
            }

            LastExitCode = ProcessRunner.Run(args[0], args.Skip(1).ToArray());
        }
    }

    private bool TryRunBuiltin(IReadOnlyList<string> args, out int exitCode)
    {
        exitCode = 0;
        switch (args[0].ToLowerInvariant())
        {
            case "exit":
                var code = args.Count > 1 && int.TryParse(args[1], out var c) ? c : LastExitCode;
                exitCode = -code - 1;
                return true;
            case "cd":
                exitCode = ChangeDirectory(args.Count > 1 ? args[1] : null);
                return true;
            case "pwd":
                Console.Out.WriteLine(Environment.CurrentDirectory);
                return true;
            default:
                return false;
        }
    }

    private int ChangeDirectory(string? path)
    {
        var target = path switch
        {
            null => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "-" => _previousDirectory ?? Environment.CurrentDirectory,
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

            _previousDirectory = Environment.CurrentDirectory;
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

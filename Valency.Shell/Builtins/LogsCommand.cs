using System.Diagnostics;
using Valency.Shell.Core.Builtins;
using Valency.Shell.Logging;

namespace Valency.Shell.Builtins;

public sealed class LogsCommand : IBuiltinCommand
{
    private readonly string _logFilePath;
    private readonly int _udpPort;

    public LogsCommand(string logFilePath, int udpPort)
    {
        _logFilePath = logFilePath;
        _udpPort = udpPort;
    }

    public string Name => BuiltinNames.Logs;

    public int Execute(IReadOnlyList<string> args, IShellContext context)
    {
        var tail = -1;
        var head = -1;
        string? level = null;
        var follow = false;

        for (var i = 1; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    PrintHelp();
                    return 0;
                case "-f" or "--follow":
                    follow = true;
                    break;
                case "-n" or "--tail":
                    if (!TryReadInt(args, ref i, out tail))
                        return 2;
                    break;
                case "--head":
                    if (!TryReadInt(args, ref i, out head))
                        return 2;
                    break;
                case "--level":
                    if (i + 1 >= args.Count)
                    {
                        Console.Error.WriteLine("logs: --level 需要一个值");
                        return 2;
                    }
                    level = args[++i];
                    break;
                default:
                    Console.Error.WriteLine($"logs: 未知参数 '{args[i]}'，用 logs --help 查看帮助");
                    return 2;
            }
        }

        if (follow)
            return Follow();

        return PrintRange(head, tail, level);
    }

    private int PrintRange(int head, int tail, string? level)
    {
        if (!File.Exists(_logFilePath))
        {
            Console.Error.WriteLine($"logs: 日志文件不存在: {_logFilePath}");
            return 1;
        }

        var lines = LogFileReader.Read(_logFilePath);
        var result = LogFileReader.Filter(lines, level, head, tail);

        foreach (var line in result)
            PrintColored(line.Raw);

        Console.Out.WriteLine($"共 {result.Count} 行");
        return 0;
    }

    private int Follow()
    {
        var viewer = ResolveViewerPath();
        if (viewer is null)
        {
            Console.Out.WriteLine("未找到日志查看器，可手动运行: dotnet run --project Valency.Shell.LogViewer");
            return 1;
        }

        if (OperatingSystem.IsWindows())
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start \"Valency Log\" \"{viewer}\" --udp {_udpPort}",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(startInfo);
            Console.Out.WriteLine("已在新窗口打开日志查看器 (UDP 实时模式)");
        }
        else
        {
            Console.Out.WriteLine($"请在另一个终端运行: \"{viewer}\" --udp {_udpPort}");
        }

        return 0;
    }

    private static bool TryReadInt(IReadOnlyList<string> args, ref int i, out int value)
    {
        value = -1;
        if (i + 1 >= args.Count || !int.TryParse(args[i + 1], out value))
        {
            Console.Error.WriteLine($"logs: {args[i]} 需要一个数字");
            return false;
        }
        i++;
        return true;
    }

    private static string? ResolveViewerPath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "Valency.Shell.LogViewer.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private static void PrintColored(string raw)
    {
        if (Console.IsOutputRedirected)
        {
            Console.Out.WriteLine(raw);
            return;
        }

        var color = raw switch
        {
            _ when raw.Contains("[FTL]") => ConsoleColor.Magenta,
            _ when raw.Contains("[ERR]") => ConsoleColor.Red,
            _ when raw.Contains("[WRN]") => ConsoleColor.Yellow,
            _ when raw.Contains("[DBG]") || raw.Contains("[VRB]") => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray,
        };

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Out.WriteLine(raw);
        Console.ForegroundColor = previous;
    }

    private static void PrintHelp()
    {
        Console.Out.WriteLine("用法: logs [选项]");
        Console.Out.WriteLine("默认显示当前会话的全部日志（从开始到现在）。");
        Console.Out.WriteLine();
        Console.Out.WriteLine("  -h, --help        显示本帮助");
        Console.Out.WriteLine("  -n, --tail N      只显示最近 N 行");
        Console.Out.WriteLine("      --head N      只显示最前 N 行");
        Console.Out.WriteLine("      --level <lvl> 按级别过滤: debug|info|warn|error|fatal");
        Console.Out.WriteLine("  -f, --follow      在独立窗口实时跟随日志 (UDP)");
    }
}

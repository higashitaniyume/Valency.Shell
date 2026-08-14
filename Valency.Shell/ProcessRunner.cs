using System.Diagnostics;

namespace Valency.Shell;

public static class ProcessRunner
{
    private static ProcessStartInfo CreateStartInfo(string resolved, IReadOnlyList<string> arguments)
    {
        var extension = Path.GetExtension(resolved);
        if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var inner = Quote(resolved);
            foreach (var arg in arguments)
                inner += " " + Quote(arg);

            return new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = "/c \"" + inner + "\"",
                UseShellExecute = false,
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = resolved,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);
        return startInfo;
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\\\"") + "\""
            : value;
    }

    public static int Run(string command, IReadOnlyList<string> arguments)
    {
        var resolved = PathResolver.Resolve(command);
        if (resolved is null)
        {
            Console.Error.WriteLine($"'{command}' 不是可识别的命令或可执行文件");
            return 1;
        }

        var startInfo = CreateStartInfo(resolved, arguments);

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Console.Error.WriteLine($"无法启动进程: {command}");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"启动 '{command}' 失败: {ex.Message}");
            return 1;
        }
    }
}

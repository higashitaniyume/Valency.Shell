using System.Diagnostics;

namespace Valency.Shell;

public static class ProcessRunner
{
    public static int Run(string command, IReadOnlyList<string> arguments)
    {
        var resolved = PathResolver.Resolve(command);
        if (resolved is null)
        {
            Console.Error.WriteLine($"'{command}' 不是可识别的命令或可执行文件");
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = resolved,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

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

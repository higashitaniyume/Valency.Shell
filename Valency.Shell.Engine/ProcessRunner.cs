using System.Diagnostics;
using Serilog;
using Valency.Shell.Core.Resolution;

namespace Valency.Shell.Engine;

public static class ProcessRunner
{
    public static int Run(string command, IReadOnlyList<string> arguments, ILogger? logger = null)
    {
        var resolved = PathResolver.Resolve(command);
        if (resolved is null)
        {
            Console.Error.WriteLine($"'{command}' 不是可识别的命令或可执行文件");
            return 127;
        }
        logger?.Debug("PATH 解析: {Command} → {Path}", command, resolved);

        try
        {
            using var process = Process.Start(CreateStartInfo(resolved, arguments));
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

    public static int RunPipeline(IReadOnlyList<string[]> commands, IList<Process>? foreground = null, ILogger? logger = null)
    {
        var processes = new List<Process>();

        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var name = commands[i][0];
                var args = commands[i].Skip(1).ToArray();
                var resolved = PathResolver.Resolve(name);
                if (resolved is null)
                {
                    Console.Error.WriteLine($"'{name}' 不是可识别的命令或可执行文件");
                    return 127;
                }
                logger?.Debug("PATH 解析: {Command} → {Path}", name, resolved);

                var startInfo = CreateStartInfo(resolved, args);
                if (i > 0) startInfo.RedirectStandardInput = true;
                if (i < commands.Count - 1) startInfo.RedirectStandardOutput = true;

                var process = Process.Start(startInfo);
                if (process is null)
                {
                    Console.Error.WriteLine($"无法启动进程: {name}");
                    return 1;
                }
                logger?.Debug("管道启动进程 {Index}: {Command} (PID {Pid})", i, name, process.Id);
                processes.Add(process);
                foreground?.Add(process);
            }

            var bridges = new List<Task>();
            for (var i = 0; i < processes.Count - 1; i++)
            {
                var left = processes[i];
                var right = processes[i + 1];
                bridges.Add(BridgeAsync(left.StandardOutput, right.StandardInput));
            }

            foreach (var process in processes)
                process.WaitForExit();

            return processes[^1].ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"启动管道失败: {ex.Message}");
            return 1;
        }
        finally
        {
            foreach (var process in processes)
            {
                foreground?.Remove(process);
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                }
                process.Dispose();
            }
        }
    }

    public static string RunPipelineCaptured(
        IReadOnlyList<string[]> commands,
        IList<Process>? foreground,
        ILogger? logger,
        out int exitCode)
    {
        var processes = new List<Process>();
        exitCode = 127;

        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var name = commands[i][0];
                var args = commands[i].Skip(1).ToArray();
                var resolved = PathResolver.Resolve(name);
                if (resolved is null)
                {
                    Console.Error.WriteLine($"'{name}' 不是可识别的命令或可执行文件");
                    return string.Empty;
                }
                logger?.Debug("PATH 解析: {Command} → {Path}", name, resolved);

                var startInfo = CreateStartInfo(resolved, args);
                if (i > 0) startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;

                var process = Process.Start(startInfo);
                if (process is null)
                {
                    Console.Error.WriteLine($"无法启动进程: {name}");
                    exitCode = 1;
                    return string.Empty;
                }
                logger?.Debug("管道启动进程 {Index}: {Command} (PID {Pid})", i, name, process.Id);
                processes.Add(process);
                foreground?.Add(process);
            }

            for (var i = 0; i < processes.Count - 1; i++)
                _ = BridgeAsync(processes[i].StandardOutput, processes[i + 1].StandardInput);

            var lastOutput = processes[^1].StandardOutput.ReadToEndAsync();

            foreach (var process in processes)
                process.WaitForExit();

            exitCode = processes[^1].ExitCode;
            return lastOutput.Result;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"启动管道失败: {ex.Message}");
            exitCode = 1;
            return string.Empty;
        }
        finally
        {
            foreach (var process in processes)
            {
                foreground?.Remove(process);
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                }
                process.Dispose();
            }
        }
    }

    public static BackgroundJob? StartBackground(string command, IReadOnlyList<string> arguments, ILogger? logger = null)
    {
        var resolved = PathResolver.Resolve(command);
        if (resolved is null)
        {
            Console.Error.WriteLine($"'{command}' 不是可识别的命令或可执行文件");
            return null;
        }
        logger?.Debug("PATH 解析: {Command} → {Path}", command, resolved);

        var startInfo = CreateStartInfo(resolved, arguments);
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                Console.Error.WriteLine($"无法启动进程: {command}");
                return null;
            }

            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            return new BackgroundJob
            {
                Command = command,
                Process = process,
                Output = output,
                Error = error,
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.Error.WriteLine($"启动 '{command}' 失败: {ex.Message}");
            return null;
        }
    }

    private static async Task BridgeAsync(StreamReader from, StreamWriter to)
    {
        try
        {
            await from.BaseStream.CopyToAsync(to.BaseStream).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
        finally
        {
            try
            {
                to.Close();
            }
            catch (Exception)
            {
            }
        }
    }

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
}

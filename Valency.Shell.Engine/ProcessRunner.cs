using System.Diagnostics;
using Serilog;
using Valency.Shell.Core.Resolution;

namespace Valency.Shell.Engine;

public enum RedirectKind
{
    Input,
    Output,
    Append,
    DupOutput,
}

public readonly record struct RedirectSpec(int Fd, RedirectKind Kind, string Target);

public static class ProcessRunner
{
    public static int Run(
        string command,
        IReadOnlyList<string> arguments,
        IReadOnlyList<RedirectSpec>? redirects = null,
        string? workingDirectory = null,
        ILogger? logger = null)
    {
        var log = logger?.ForContext("Src", "proc");
        var resolved = PathResolver.Resolve(command, workingDirectory);
        if (resolved is null)
        {
            log?.Error(Resources.LogCommandNotFound, command);
            Console.Error.WriteLine(string.Format(Resources.CommandNotFound, command));
            return 127;
        }
        log?.Debug(Resources.PathResolved, command, resolved);

        try
        {
            using var process = Process.Start(CreateStartInfo(resolved, arguments, redirects, workingDirectory));
            if (process is null)
            {
                log?.Error(Resources.LogCannotStartProcess, command);
                Console.Error.WriteLine(string.Format(Resources.CannotStartProcess, command));
                return 1;
            }

            var pumps = PumpRedirects(process, redirects);
            process.WaitForExit();
            foreach (var task in pumps)
                task.Wait();
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            log?.Error(Resources.LogStartFailed, command, ex.Message);
            Console.Error.WriteLine(string.Format(Resources.StartFailed, command, ex.Message));
            return 1;
        }
    }

    public static int RunPipeline(
        IReadOnlyList<string[]> commands,
        IList<Process>? foreground = null,
        IReadOnlyList<RedirectSpec>? redirects = null,
        string? workingDirectory = null,
        ILogger? logger = null)
    {
        var log = logger?.ForContext("Src", "proc");
        var processes = new List<Process>();

        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var name = commands[i][0];
                var args = commands[i].Skip(1).ToArray();
                var resolved = PathResolver.Resolve(name, workingDirectory);
                if (resolved is null)
                {
                    log?.Error(Resources.LogCommandNotFound, name);
                    Console.Error.WriteLine(string.Format(Resources.CommandNotFound, name));
                    return 127;
                }
                log?.Debug(Resources.PathResolved, name, resolved);

                var stageRedirects = StageRedirects(redirects, i, commands.Count);
                var startInfo = CreateStartInfo(resolved, args, stageRedirects, workingDirectory);
                if (i > 0) startInfo.RedirectStandardInput = true;
                if (i < commands.Count - 1) startInfo.RedirectStandardOutput = true;

                var process = Process.Start(startInfo);
                if (process is null)
                {
                    log?.Error(Resources.LogCannotStartProcess, name);
                    Console.Error.WriteLine(string.Format(Resources.CannotStartProcess, name));
                    return 1;
                }
                log?.Debug(Resources.PipelineProcessStarted, i, name, process.Id);
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

            var stagePumps = PumpStageRedirects(processes, redirects);

            foreach (var process in processes)
                process.WaitForExit();

            foreach (var task in stagePumps)
                task.Wait();

            return processes[^1].ExitCode;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            log?.Error(Resources.LogPipelineStartFailed, ex.Message);
            Console.Error.WriteLine(string.Format(Resources.PipelineStartFailed, ex.Message));
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
        out int exitCode,
        string? workingDirectory = null)
    {
        var log = logger?.ForContext("Src", "proc");
        var processes = new List<Process>();
        exitCode = 127;

        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var name = commands[i][0];
                var args = commands[i].Skip(1).ToArray();
                var resolved = PathResolver.Resolve(name, workingDirectory);
                if (resolved is null)
                {
                    log?.Error(Resources.LogCommandNotFound, name);
                    Console.Error.WriteLine(string.Format(Resources.CommandNotFound, name));
                    return string.Empty;
                }
                log?.Debug(Resources.PathResolved, name, resolved);

                var startInfo = CreateStartInfo(resolved, args, null, workingDirectory);
                if (i > 0) startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;

                var process = Process.Start(startInfo);
                if (process is null)
                {
                    log?.Error(Resources.LogCannotStartProcess, name);
                    Console.Error.WriteLine(string.Format(Resources.CannotStartProcess, name));
                    exitCode = 1;
                    return string.Empty;
                }
                log?.Debug(Resources.PipelineProcessStarted, i, name, process.Id);
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
            log?.Error(Resources.LogPipelineStartFailed, ex.Message);
            Console.Error.WriteLine(string.Format(Resources.PipelineStartFailed, ex.Message));
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

    public static BackgroundJob? StartBackground(
        string command,
        IReadOnlyList<string> arguments,
        ILogger? logger = null,
        string? workingDirectory = null)
    {
        var log = logger?.ForContext("Src", "proc");
        var resolved = PathResolver.Resolve(command, workingDirectory);
        if (resolved is null)
        {
            log?.Error(Resources.LogCommandNotFound, command);
            Console.Error.WriteLine(string.Format(Resources.CommandNotFound, command));
            return null;
        }
        log?.Debug(Resources.PathResolved, command, resolved);

        var startInfo = CreateStartInfo(resolved, arguments, null, workingDirectory);
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
            {
                log?.Error(Resources.LogCannotStartProcess, command);
                Console.Error.WriteLine(string.Format(Resources.CannotStartProcess, command));
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
            log?.Error(Resources.LogStartFailed, command, ex.Message);
            Console.Error.WriteLine(string.Format(Resources.StartFailed, command, ex.Message));
            return null;
        }
    }

    private static IReadOnlyList<RedirectSpec>? StageRedirects(
        IReadOnlyList<RedirectSpec>? redirects, int index, int total)
    {
        if (redirects is null || redirects.Count == 0)
            return null;
        return index == 0
            ? redirects.Where(r => r.Kind == RedirectKind.Input).ToArray()
            : index == total - 1
                ? redirects.Where(r => r.Kind != RedirectKind.Input).ToArray()
                : null;
    }

    private static IReadOnlyList<Task> PumpStageRedirects(IReadOnlyList<Process> processes, IReadOnlyList<RedirectSpec>? redirects)
    {
        if (redirects is null || redirects.Count == 0)
            return [];
        var tasks = new List<Task>();
        AddInputPump(tasks, processes[0], redirects);
        tasks.AddRange(PumpOutput(processes[^1], redirects));
        return tasks;
    }

    private static IReadOnlyList<Task> PumpRedirects(Process process, IReadOnlyList<RedirectSpec>? redirects)
    {
        if (redirects is null || redirects.Count == 0)
            return [];
        var tasks = new List<Task>();
        AddInputPump(tasks, process, redirects);
        tasks.AddRange(PumpOutput(process, redirects));
        return tasks;
    }

    private static void AddInputPump(List<Task> tasks, Process process, IReadOnlyList<RedirectSpec> redirects)
    {
        RedirectSpec? input = null;
        foreach (var redirect in redirects)
        {
            if (redirect.Kind == RedirectKind.Input && !string.IsNullOrEmpty(redirect.Target))
            {
                input = redirect;
                break;
            }
        }

        if (input is not { } spec)
            return;

        tasks.Add(Task.Run(() =>
        {
            try
            {
                using var fs = new FileStream(spec.Target, FileMode.Open, FileAccess.Read);
                fs.CopyTo(process.StandardInput.BaseStream);
            }
            catch (Exception)
            {
            }
            finally
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch (Exception)
                {
                }
            }
        }));
    }

    private static IReadOnlyList<Task> PumpOutput(Process process, IReadOnlyList<RedirectSpec> redirects)
    {
        var stdoutTarget = ResolveStdoutTarget(redirects);
        var stderrTarget = ResolveStderrTarget(redirects, stdoutTarget);

        var tasks = new List<Task>();
        if (stdoutTarget is not null)
            tasks.Add(PumpStream(process.StandardOutput, stdoutTarget.Value));
        if (stderrTarget is not null)
            tasks.Add(PumpStream(process.StandardError, stderrTarget.Value));
        return tasks;
    }

    private static (string Path, bool Append)? ResolveStdoutTarget(IReadOnlyList<RedirectSpec> redirects)
    {
        foreach (var r in redirects)
        {
            if (r.Fd == 1 && r.Kind is RedirectKind.Output or RedirectKind.Append)
                return (r.Target, r.Kind == RedirectKind.Append);
        }
        return null;
    }

    private static (string Path, bool Append)? ResolveStderrTarget(
        IReadOnlyList<RedirectSpec> redirects, (string Path, bool Append)? stdoutTarget)
    {
        foreach (var r in redirects)
        {
            if (r.Fd == 2 && r.Kind is RedirectKind.Output or RedirectKind.Append)
                return (r.Target, r.Kind == RedirectKind.Append);
            if (r.Fd == 2 && r.Kind == RedirectKind.DupOutput && stdoutTarget is not null)
                return stdoutTarget;
        }
        return null;
    }

    private static Task PumpStream(StreamReader from, (string Path, bool Append) target)
    {
        return Task.Run(() =>
        {
            try
            {
                using var fs = new FileStream(
                    target.Path,
                    target.Append ? FileMode.Append : FileMode.Create,
                    FileAccess.Write);
                from.BaseStream.CopyTo(fs);
            }
            catch (Exception)
            {
            }
        });
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

    private static ProcessStartInfo CreateStartInfo(
        string resolved,
        IReadOnlyList<string> arguments,
        IReadOnlyList<RedirectSpec>? redirects,
        string? workingDirectory)
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
                WorkingDirectory = workingDirectory ?? string.Empty,
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = resolved,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };

        if (redirects is not null && redirects.Count > 0)
        {
            if (redirects.Any(r => r.Kind == RedirectKind.Input))
                startInfo.RedirectStandardInput = true;
            if (redirects.Any(r => r.Kind is RedirectKind.Output or RedirectKind.Append or RedirectKind.DupOutput && r.Fd == 1))
                startInfo.RedirectStandardOutput = true;
            if (redirects.Any(r => r.Kind is RedirectKind.Output or RedirectKind.Append && r.Fd == 2))
                startInfo.RedirectStandardError = true;
        }

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

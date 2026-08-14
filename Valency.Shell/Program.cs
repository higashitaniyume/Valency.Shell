using Serilog;
using Valency.Shell;
using Valency.Shell.Builtins;
using Valency.Shell.Logging;

Console.CancelKeyPress += (_, e) => e.Cancel = true;

var logFilePath = ShellLogging.CreateLogFilePath(DateTimeOffset.Now);
Log.Logger = ShellLogging.CreateShellLogger(logFilePath);
Log.Information(
    "Valency.Shell 启动 PID={Pid} 日志文件={LogFile} UDP端口={UdpPort}",
    Environment.ProcessId, logFilePath, ShellLogging.GetUdpPort());

var exitCode = 1;
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
try
{
    var builtins = new BuiltinRegistry(
        new ExitCommand(),
        new CdCommand(),
        new PwdCommand(),
        new JobsCommand(),
        new LogsCommand(logFilePath, ShellLogging.GetUdpPort()));

    var shell = new Shell(builtins, Log.Logger);
    exitCode = shell.Run();
}
finally
{
    stopwatch.Stop();
    Log.Information(
        "Valency.Shell 退出 退出码 {ExitCode} 运行时长 {DurationMs}ms",
        exitCode, stopwatch.ElapsedMilliseconds);
    Log.CloseAndFlush();
}

return exitCode;

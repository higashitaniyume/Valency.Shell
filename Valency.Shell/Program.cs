using Serilog;
using Valency.Shell;
using Valency.Shell.Builtins;
using Valency.Shell.Logging;
using Valency.Shell.Prompting;

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
    var promptSettings = LoadPromptSettings();
    var promptFormatter = new PromptFormatter();

    var help = new HelpCommand();
    var builtins = new BuiltinRegistry(
        new ExitCommand(),
        new CdCommand(),
        new PwdCommand(),
        new JobsCommand(),
        new LogsCommand(logFilePath, ShellLogging.GetUdpPort()),
        new PromptCommand(promptSettings),
        new GrepCommand(),
        help);
    help.Registry = builtins;

    var shell = new Shell(builtins, Log.Logger, promptFormatter, promptSettings);
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

static PromptSettings LoadPromptSettings()
{
    var settings = new PromptSettings();

    var style = Environment.GetEnvironmentVariable("VALENCY_PROMPT");
    if (!string.IsNullOrWhiteSpace(style))
        settings.Style = style.ToLowerInvariant();

    var format = Environment.GetEnvironmentVariable("VALENCY_PROMPT_FORMAT");
    if (!string.IsNullOrWhiteSpace(format))
    {
        settings.CustomTemplate = format;
        if (settings.Style != PromptSettings.Custom)
            settings.Style = PromptSettings.Custom;
    }

    return settings;
}

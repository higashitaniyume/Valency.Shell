using Serilog;
using Valency.Shell;
using Valency.Shell.Builtins;
using Valency.Shell.Core.Builtins;
using Valency.Shell.Logging;
using Valency.Shell.Platform;
using Valency.Shell.Prompting;

WindowsConsole.Configure();

Console.CancelKeyPress += (_, e) => e.Cancel = true;

var logFilePath = ShellLogging.CreateLogFilePath(DateTimeOffset.Now);
Log.Logger = ShellLogging.CreateShellLogger(logFilePath);
Log.ForContext("Src", "shell").Information(
    Resources.LogStartup,
    Environment.ProcessId, logFilePath, ShellLogging.GetUdpPort());

var exitCode = 1;
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
try
{
    var promptSettings = LoadPromptSettings();
    var promptFormatter = new PromptFormatter();

    var builtins = BuiltinCommands.CreateDefault(logFilePath, ShellLogging.GetUdpPort(), promptSettings);

    var shell = new Shell(builtins, Log.Logger, promptFormatter, promptSettings);

    if (args.Length > 0 && args[0] == "-c")
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(Resources.ProgramUsage);
            return 2;
        }

        shell.ScriptName = args.Length > 2 ? args[2] : "valency";
        shell.PositionalArgs = args.Skip(3).ToArray();
        exitCode = shell.ExecuteLine(args[1]);
    }
    else if (args.Length > 0)
    {
        var scriptPath = args[0];
        shell.ScriptName = scriptPath;
        shell.PositionalArgs = args.Skip(1).ToArray();

        if (!File.Exists(scriptPath))
        {
            Console.Error.WriteLine(string.Format(Resources.ProgramScriptNotFound, scriptPath));
            return 127;
        }

        using var reader = new StreamReader(scriptPath);
        exitCode = shell.RunScript(reader);
    }
    else if (Console.IsInputRedirected)
    {
        exitCode = shell.RunScript(Console.In);
    }
    else
    {
        exitCode = shell.Run();
    }
}
finally
{
    stopwatch.Stop();
    Log.ForContext("Src", "shell").Information(
        Resources.LogShutdown,
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

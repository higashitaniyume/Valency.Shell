using System.Net.Sockets;
using Serilog;
using Serilog.Events;

namespace Valency.Shell.Logging;

public static class ShellLogging
{
    public const int DefaultUdpPort = 7310;
    public const string OutputTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u3}] [{Src}] {Message:lj}{NewLine}";

    public static string GetLogDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("VALENCY_LOG_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".valency", "logs");
    }

    public static string CreateLogFilePath(DateTimeOffset timestamp)
    {
        var dir = GetLogDirectory();
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"session-{timestamp:yyyyMMdd-HHmmss}.log");
    }

    public static int GetUdpPort()
    {
        return int.TryParse(Environment.GetEnvironmentVariable("VALENCY_LOG_PORT"), out var port)
            ? port
            : DefaultUdpPort;
    }

    public static ILogger CreateShellLogger(string logFilePath)
    {
        var port = GetUdpPort();
        var minLevel = GetLogLevel();
        var udpLevel = minLevel == LogEventLevel.Verbose ? LogEventLevel.Verbose : LogEventLevel.Information;

        return new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .WriteTo.Async(a => a.File(
                logFilePath,
                outputTemplate: OutputTemplate,
                shared: true,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 5))
            .WriteTo.Udp(
                "127.0.0.1",
                port,
                family: AddressFamily.InterNetwork,
                restrictedToMinimumLevel: udpLevel,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    public static LogEventLevel GetLogLevel()
    {
        var configured = Environment.GetEnvironmentVariable("VALENCY_LOG_LEVEL");
        return string.Equals(configured, "verbose", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(configured, "trace", StringComparison.OrdinalIgnoreCase)
            ? LogEventLevel.Verbose
            : LogEventLevel.Debug;
    }
}

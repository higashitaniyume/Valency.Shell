using Valency.Shell.Logging;

namespace Valency.Shell.Tests.Host;

public class ShellLoggingTests
{
    [Fact]
    public void GetLogDirectory_UsesEnvOverride()
    {
        var temp = Path.Combine(Path.GetTempPath(), "valency-log-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("VALENCY_LOG_DIR", temp);
        try
        {
            Assert.Equal(Path.GetFullPath(temp), ShellLogging.GetLogDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable("VALENCY_LOG_DIR", null);
        }
    }

    [Fact]
    public void CreateLogFilePath_CreatesSessionFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), "valency-log-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("VALENCY_LOG_DIR", temp);
        try
        {
            var timestamp = new DateTimeOffset(2026, 8, 14, 10, 30, 0, TimeSpan.Zero);
            var path = ShellLogging.CreateLogFilePath(timestamp);

            Assert.StartsWith(Path.GetFullPath(temp), path);
            Assert.EndsWith("session-20260814-103000.log", path);
            Assert.True(Directory.Exists(Path.GetFullPath(temp)));
        }
        finally
        {
            Environment.SetEnvironmentVariable("VALENCY_LOG_DIR", null);
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }
}

using Valency.Shell;

namespace Valency.Shell.Tests;

public class PathResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalPath;

    public PathResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "valency-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _originalPath = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", _tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Resolve_FindsExeOnPath_ByPathExt()
    {
        var exe = Path.Combine(_tempDir, "mytool.exe");
        File.WriteAllText(exe, "");

        Assert.Equal(exe, PathResolver.Resolve("mytool"), ignoreCase: true);
    }

    [Fact]
    public void Resolve_SkipsExtensionlessFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "shim"), "");
        File.WriteAllText(Path.Combine(_tempDir, "shim.cmd"), "");

        var resolved = PathResolver.Resolve("shim");
        Assert.EndsWith(".cmd", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ExplicitPath_ReturnsFullPath()
    {
        var exe = Path.Combine(_tempDir, "direct.exe");
        File.WriteAllText(exe, "");

        Assert.Equal(exe, PathResolver.Resolve(exe));
    }

    [Fact]
    public void Resolve_Unknown_ReturnsNull()
    {
        Assert.Null(PathResolver.Resolve("definitely-not-a-command-xyz"));
    }
}

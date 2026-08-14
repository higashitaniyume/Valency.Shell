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

    [Fact]
    public void Resolve_WithoutPathExt_MatchesBareName()
    {
        var originalPathExt = Environment.GetEnvironmentVariable("PATHEXT");
        var bare = Path.Combine(_tempDir, "mytool");
        File.WriteAllText(bare, "");
        Environment.SetEnvironmentVariable("PATHEXT", null);
        try
        {
            var resolved = PathResolver.Resolve("mytool", File.Exists);
            Assert.Equal(bare, resolved, ignoreCase: true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATHEXT", originalPathExt);
        }
    }

    [Fact]
    public void Resolve_UsesInjectedExecutablePredicate()
    {
        // On Unix a file without an execute bit is not a valid command; the
        // injectable predicate models that (DefaultIsExecutable checks the bit).
        var bare = Path.Combine(_tempDir, "tool.sh");
        File.WriteAllText(bare, "");
        var originalPathExt = Environment.GetEnvironmentVariable("PATHEXT");
        Environment.SetEnvironmentVariable("PATHEXT", null);
        try
        {
            Assert.Null(PathResolver.Resolve("tool.sh", _ => false));
            Assert.Equal(
                bare,
                PathResolver.Resolve("tool.sh", p => File.Exists(p) && p.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)),
                ignoreCase: true);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATHEXT", originalPathExt);
        }
    }
}

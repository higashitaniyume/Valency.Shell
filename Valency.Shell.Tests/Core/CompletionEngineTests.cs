using Valency.Shell.Core.Completion;

namespace Valency.Shell.Tests.Core;

public class CompletionEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalPath;
    private readonly string? _originalPathExt;

    public CompletionEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "valency-complete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _originalPath = Environment.GetEnvironmentVariable("PATH");
        _originalPathExt = Environment.GetEnvironmentVariable("PATHEXT");
        Environment.SetEnvironmentVariable("PATH", _tempDir);
        if (OperatingSystem.IsWindows())
            Environment.SetEnvironmentVariable("PATHEXT", ".EXE");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        Environment.SetEnvironmentVariable("PATHEXT", _originalPathExt);
        Directory.Delete(_tempDir, recursive: true);
    }

    private static CompletionEngine Create(params string[] builtins) => new(builtins);

    [Fact]
    public void Complete_CommandPosition_MatchesBuiltin()
    {
        var engine = Create("grep", "help", "logs");
        var result = engine.Complete("gr", 2);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsCommand);
        Assert.Equal(0, result.Value.Start);
        Assert.Contains("grep", result.Value.Candidates);
    }

    [Fact]
    public void Complete_CommandPosition_MatchesPathExecutable()
    {
        File.WriteAllText(Path.Combine(_tempDir, "mytool.exe"), "");
        File.WriteAllText(Path.Combine(_tempDir, "other.exe"), "");

        var engine = Create();
        var result = engine.Complete("myt", 3);

        Assert.NotNull(result);
        Assert.True(result!.Value.IsCommand);
        Assert.Contains("mytool", result.Value.Candidates);
    }

    [Fact]
    public void Complete_AfterSeparator_IsCommandPosition()
    {
        var engine = Create("grep");
        var result = engine.Complete("echo x | gr", 11);
        Assert.NotNull(result);
        Assert.True(result!.Value.IsCommand);
        Assert.Contains("grep", result!.Value.Candidates);
    }

    [Fact]
    public void Complete_PathPosition_ListsFilesAndDirs()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "somedir"));
        File.WriteAllText(Path.Combine(_tempDir, "somefile.txt"), "");

        var engine = Create();
        var prefix = Path.Combine(_tempDir, "some");
        var result = engine.Complete(prefix, prefix.Length);

        Assert.NotNull(result);
        Assert.False(result!.Value.IsCommand);
        var sep = Path.DirectorySeparatorChar;
        Assert.Contains(_tempDir + sep + "somedir" + sep, result.Value.Candidates);
        Assert.Contains(_tempDir + sep + "somefile.txt", result.Value.Candidates);
    }

    [Fact]
    public void Complete_DirectoryCandidate_HasTrailingSeparator()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "foobar"));

        var engine = Create();
        var prefix = Path.Combine(_tempDir, "foo");
        var result = engine.Complete(prefix, prefix.Length);

        Assert.NotNull(result);
        Assert.Contains(_tempDir + Path.DirectorySeparatorChar + "foobar" + Path.DirectorySeparatorChar, result!.Value.Candidates);
    }

    [Fact]
    public void Complete_NoMatch_ReturnsNull()
    {
        var engine = Create("grep");
        Assert.Null(engine.Complete("zzz", 3));
    }

    [Fact]
    public void Complete_EmptyToken_AtCommandStart_ReturnsCommands()
    {
        var engine = Create("grep", "help");
        var result = engine.Complete("", 0);
        Assert.NotNull(result);
        Assert.True(result!.Value.IsCommand);
        Assert.NotEmpty(result.Value.Candidates);
    }
}

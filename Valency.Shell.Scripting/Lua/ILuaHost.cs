namespace Valency.Shell.Scripting.Lua;

public enum LuaRedirectMode
{
    Input,
    Output,
    Append,
    DupOutput,
}

public readonly record struct LuaRedirect(int Fd, LuaRedirectMode Mode, string Target);

public readonly record struct CaptureResult(string Output, int ExitCode);

/// <summary>A background job as seen by the Lua layer (plain data, no CLR objects leak into Lua).</summary>
public sealed record LuaJob(int Id, int Pid, string Command, string State);

/// <summary>
///     The operations the Lua layer needs from the host process. Implemented by <c>Shell</c>.
/// </summary>
public interface ILuaHost
{
    /// <summary>Runs a command (builtin → script file → external process) and returns its exit code.</summary>
    int Run(IReadOnlyList<string> argv, IReadOnlyList<LuaRedirect>? redirects);

    /// <summary>Runs a command and captures its stdout.</summary>
    CaptureResult Capture(IReadOnlyList<string> argv);

    /// <summary>Runs a pipeline of external commands; the last stage may be a builtin.</summary>
    int Pipeline(IReadOnlyList<string[]> stages, IReadOnlyList<LuaRedirect>? redirects);

    /// <summary>Runs a pipeline and captures the last stage's stdout.</summary>
    CaptureResult CapturePipeline(IReadOnlyList<string[]> stages);

    /// <summary>Starts a background job; returns its job id, or null when it could not start.</summary>
    int? Spawn(IReadOnlyList<string> argv);

    /// <summary>Snapshot of tracked background jobs for the object-mode jobs() function.</summary>
    IReadOnlyList<LuaJob> GetJobs();

    /// <summary>True when the name resolves to a builtin, script file or PATH executable.</summary>
    bool IsCommandAvailable(string name);

    int LastExitCode { get; }

    void RequestExit(int code);

    bool ExitRequested { get; }

    int RequestedExitCode { get; }

    string CurrentDirectory { get; set; }
}

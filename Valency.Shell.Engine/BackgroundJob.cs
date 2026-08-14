using System.Diagnostics;

namespace Valency.Shell.Engine;

public enum BackgroundJobState
{
    Running,
    Completed,
}

public sealed class BackgroundJob
{
    public int Id { get; set; }
    public required string Command { get; init; }
    public required Process Process { get; init; }
    public required Task<string> Output { get; init; }
    public required Task<string> Error { get; init; }

    public BackgroundJobState State { get; private set; } = BackgroundJobState.Running;
    public int ExitCode { get; private set; }

    public bool TryComplete()
    {
        if (State != BackgroundJobState.Running || !Process.HasExited)
            return false;

        ExitCode = Process.ExitCode;
        State = BackgroundJobState.Completed;
        return true;
    }
}

namespace Valency.Shell.Builtins;

public interface IShellContext
{
    int LastExitCode { get; set; }
    string? PreviousDirectory { get; set; }
    bool ExitRequested { get; }
    int RequestedExitCode { get; }
    void RequestExit(int exitCode);
    void PrintJobs();
}

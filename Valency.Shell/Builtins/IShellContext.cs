namespace Valency.Shell.Builtins;

public interface IShellContext
{
    int LastExitCode { get; set; }
    string? PreviousDirectory { get; set; }
    string CurrentDirectory { get; set; }
    bool ExitRequested { get; }
    int RequestedExitCode { get; }
    void RequestExit(int exitCode);
    void PrintJobs();
    TextReader? PipelineInput { get; }
    string? GetVariable(string name);
    void SetVariable(string name, string value, bool exported);
    void ExportVariable(string name);
    void UnsetVariable(string name);
    void ShiftArguments(int count);
    int RunScriptFile(string path);
}

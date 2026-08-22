using System.Globalization;
using System.Resources;

namespace Valency.Shell;

internal static class Resources
{
	private static readonly ResourceManager Manager = new(
		"Valency.Shell.Properties.Resources",
		typeof(Resources).Assembly);

	internal static string ArgParserUnknownOption => Get("ArgParserUnknownOption");
	internal static string ArgParserMissingValue => Get("ArgParserMissingValue");
	internal static string CdSummary => Get("CdSummary");
	internal static string CdPositional => Get("CdPositional");
	internal static string CdPathNotFound => Get("CdPathNotFound");
	internal static string BreakSummary => Get("BreakSummary");
	internal static string ContinueSummary => Get("ContinueSummary");
	internal static string ReturnSummary => Get("ReturnSummary");
	internal static string ReturnPositional => Get("ReturnPositional");
	internal static string EchoSummary => Get("EchoSummary");
	internal static string EchoPositional => Get("EchoPositional");
	internal static string EchoNoNewline => Get("EchoNoNewline");
	internal static string EchoEnableEscapes => Get("EchoEnableEscapes");
	internal static string ExitSummary => Get("ExitSummary");
	internal static string ExitPositional => Get("ExitPositional");
	internal static string GrepSummary => Get("GrepSummary");
	internal static string GrepPositionalPattern => Get("GrepPositionalPattern");
	internal static string GrepPositionalFile => Get("GrepPositionalFile");
	internal static string GrepIgnoreCase => Get("GrepIgnoreCase");
	internal static string GrepInvertMatch => Get("GrepInvertMatch");
	internal static string GrepLineNumber => Get("GrepLineNumber");
	internal static string GrepCount => Get("GrepCount");
	internal static string GrepMissingPattern => Get("GrepMissingPattern");
	internal static string GrepFileNotFound => Get("GrepFileNotFound");
	internal static string HelpSummary => Get("HelpSummary");
	internal static string HelpPositional => Get("HelpPositional");
	internal static string HelpListTitle => Get("HelpListTitle");
	internal static string HelpUnknownCommand => Get("HelpUnknownCommand");
	internal static string HelpUsage => Get("HelpUsage");
	internal static string HelpPositionals => Get("HelpPositionals");
	internal static string HelpOptions => Get("HelpOptions");
	internal static string JobsSummary => Get("JobsSummary");
	internal static string LogsSummary => Get("LogsSummary");
	internal static string LogsTail => Get("LogsTail");
	internal static string LogsHead => Get("LogsHead");
	internal static string LogsLevel => Get("LogsLevel");
	internal static string LogsFollow => Get("LogsFollow");
	internal static string LogsFileNotFound => Get("LogsFileNotFound");
	internal static string LogsTotalLines => Get("LogsTotalLines");
	internal static string LogsViewerNotFound => Get("LogsViewerNotFound");
	internal static string LogsOpenedNewWindow => Get("LogsOpenedNewWindow");
	internal static string LogsRunInAnotherTerminal => Get("LogsRunInAnotherTerminal");
	internal static string PromptSummary => Get("PromptSummary");
	internal static string PromptPositionalStyle => Get("PromptPositionalStyle");
	internal static string PromptPositionalTemplate => Get("PromptPositionalTemplate");
	internal static string PromptCurrentStyle => Get("PromptCurrentStyle");
	internal static string PromptCustomTemplate => Get("PromptCustomTemplate");
	internal static string PromptUnknownStyle => Get("PromptUnknownStyle");
	internal static string PwdSummary => Get("PwdSummary");
	internal static string SourceSummary => Get("SourceSummary");
	internal static string SourcePositional => Get("SourcePositional");
	internal static string SourceNeedFile => Get("SourceNeedFile");
	internal static string TestSummary => Get("TestSummary");
	internal static string TestSummaryBracket => Get("TestSummaryBracket");
	internal static string TestPositional => Get("TestPositional");
	internal static string TestMissingBracket => Get("TestMissingBracket");
	internal static string TfAlwaysSucceed => Get("TfAlwaysSucceed");
	internal static string TfAlwaysFail => Get("TfAlwaysFail");
	internal static string ExportSummary => Get("ExportSummary");
	internal static string ExportPositional => Get("ExportPositional");
	internal static string UnsetSummary => Get("UnsetSummary");
	internal static string UnsetPositional => Get("UnsetPositional");
	internal static string ReadSummary => Get("ReadSummary");
	internal static string ReadPositional => Get("ReadPositional");
	internal static string ReadNeedVariable => Get("ReadNeedVariable");
	internal static string ShiftSummary => Get("ShiftSummary");
	internal static string ShiftPositional => Get("ShiftPositional");
	internal static string ShiftInvalidCount => Get("ShiftInvalidCount");
	internal static string ShellSourceFileNotFound => Get("ShellSourceFileNotFound");
	internal static string ShellJobRunning => Get("ShellJobRunning");
	internal static string ShellJobDone => Get("ShellJobDone");
	internal static string ProgramUsage => Get("ProgramUsage");
	internal static string ProgramScriptNotFound => Get("ProgramScriptNotFound");
	internal static string LogCtrlCInterrupted => Get("LogCtrlCInterrupted");
	internal static string LogJobStarted => Get("LogJobStarted");
	internal static string LogJobCompleted => Get("LogJobCompleted");
	internal static string LogStartup => Get("LogStartup");
	internal static string LogShutdown => Get("LogShutdown");
	internal static string LogScriptFile => Get("LogScriptFile");

	private static string Get(string key)
		=> Manager.GetString(key, CultureInfo.CurrentCulture) ?? key;
}

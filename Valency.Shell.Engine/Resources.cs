using System.Globalization;
using System.Resources;

namespace Valency.Shell.Engine;

internal static class Resources
{
	private static readonly ResourceManager Manager = new(
		"Valency.Shell.Engine.Properties.Resources",
		typeof(Resources).Assembly);

	internal static string CommandNotFound => Get("CommandNotFound");
	internal static string CannotStartProcess => Get("CannotStartProcess");
	internal static string StartFailed => Get("StartFailed");
	internal static string PipelineStartFailed => Get("PipelineStartFailed");
	internal static string PathResolved => Get("PathResolved");
	internal static string PipelineProcessStarted => Get("PipelineProcessStarted");
	internal static string LogCommandNotFound => Get("LogCommandNotFound");
	internal static string LogCannotStartProcess => Get("LogCannotStartProcess");
	internal static string LogStartFailed => Get("LogStartFailed");
	internal static string LogPipelineStartFailed => Get("LogPipelineStartFailed");

	private static string Get(string key)
		=> Manager.GetString(key, CultureInfo.CurrentCulture) ?? key;
}

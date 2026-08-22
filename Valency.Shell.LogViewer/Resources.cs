using System.Globalization;
using System.Resources;

namespace Valency.Shell.LogViewer;

internal static class Resources
{
	private static readonly ResourceManager Manager = new(
		"Valency.Shell.LogViewer.Properties.Resources",
		typeof(Resources).Assembly);

	internal static string FileNeedPath => Get("FileNeedPath");
	internal static string UsageHeader => Get("UsageHeader");
	internal static string UsageUdp => Get("UsageUdp");
	internal static string UsageFile => Get("UsageFile");
	internal static string UsageTail => Get("UsageTail");
	internal static string ListeningUdp => Get("ListeningUdp");
	internal static string FollowingFile => Get("FollowingFile");

	private static string Get(string key)
		=> Manager.GetString(key, CultureInfo.CurrentCulture) ?? key;
}

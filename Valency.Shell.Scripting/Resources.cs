using System.Globalization;
using System.Resources;

namespace Valency.Shell.Scripting;

internal static class Resources
{
	private static readonly ResourceManager Manager = new(
		"Valency.Shell.Scripting.Properties.Resources",
		typeof(Resources).Assembly);

	internal static string LuaArgNotString => Get("LuaArgNotString");
	internal static string LuaMissingCommand => Get("LuaMissingCommand");
	internal static string LuaUnknownOptionKey => Get("LuaUnknownOptionKey");
	internal static string LuaOptionValueString => Get("LuaOptionValueString");
	internal static string LuaMergeNeedsOut => Get("LuaMergeNeedsOut");
	internal static string LuaPipeStageEmpty => Get("LuaPipeStageEmpty");
	internal static string LuaPipeStageForm => Get("LuaPipeStageForm");
	internal static string LogLuaChunk => Get("LogLuaChunk");
	internal static string LogLuaSyntaxError => Get("LogLuaSyntaxError");
	internal static string LogLuaRuntimeError => Get("LogLuaRuntimeError");

	private static string Get(string key)
		=> Manager.GetString(key, CultureInfo.CurrentCulture) ?? key;
}

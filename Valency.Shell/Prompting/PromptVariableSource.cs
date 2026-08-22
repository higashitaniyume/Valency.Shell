using Valency.Shell.Core.Expansion;

namespace Valency.Shell.Prompting;

public sealed class PromptVariableSource : IVariableSource
{
	private readonly Func<bool> _isAdmin;
	private readonly string _user;
	private readonly string _host;

	public PromptVariableSource(Func<bool> isAdmin)
	{
		_isAdmin = isAdmin;
		_user = Environment.UserName;
		_host = Environment.MachineName;
	}

	public bool TryGet(string name, out string? value)
	{
		switch (name.ToUpperInvariant())
		{
			case "PWD":
				value = AbbreviateHome(Environment.CurrentDirectory);
				return true;
			case "USER":
				value = _user;
				return true;
			case "HOST":
			case "HOSTNAME":
				value = _host;
				return true;
			case "SHARP":
				value = _isAdmin() ? "#" : "$";
				return true;
			case "CONN":
				value = "@";
				return true;
			default:
				value = Environment.GetEnvironmentVariable(name);
				return value is not null;
		}
	}

	public static string AbbreviateHome(string path)
	{
		var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (string.IsNullOrEmpty(home))
			return path;
		if (string.Equals(path, home, StringComparison.OrdinalIgnoreCase))
			return "~";

		var homeWithSep = home.EndsWith(Path.DirectorySeparatorChar) || home.EndsWith(Path.AltDirectorySeparatorChar)
			? home
			: home + Path.DirectorySeparatorChar;

		if (path.StartsWith(homeWithSep, StringComparison.OrdinalIgnoreCase))
			return "~" + path[home.Length..];

		return path;
	}
}

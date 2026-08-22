namespace Valency.Shell.Core.Resolution;

public static class PathResolver
{
	public static string? Resolve(string command, string? baseDirectory = null)
	{
		return Resolve(command, IsExecutable, baseDirectory);
	}

	public static string? Resolve(string command, Func<string, bool> isExecutable, string? baseDirectory = null)
	{
		var baseDir = baseDirectory ?? Environment.CurrentDirectory;

		if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
		{
			var full = Path.GetFullPath(command, baseDir);
			return isExecutable(full) ? full : null;
		}

		var hasExtension = !string.IsNullOrEmpty(Path.GetExtension(command));

		string[] candidates;
		var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
		if (!hasExtension && !string.IsNullOrWhiteSpace(pathExt))
		{
			candidates = pathExt
				.Split(';', StringSplitOptions.RemoveEmptyEntries)
				.Select(ext => command + ext)
				.ToArray();
		}
		else
		{
			candidates = [command];
		}

		var searchDirs = new[] { baseDir }
			.Concat((Environment.GetEnvironmentVariable("PATH") ?? "")
				.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

		foreach (var dir in searchDirs)
		{
			foreach (var candidate in candidates)
			{
				try
				{
					var full = Path.Combine(dir, candidate);
					if (isExecutable(full))
						return full;
				}
				catch (ArgumentException)
				{
				}
			}
		}

		return null;
	}

	private static bool IsExecutable(string path)
	{
		if (!File.Exists(path))
			return false;
		if (OperatingSystem.IsWindows())
			return true;

		try
		{
			var mode = File.GetUnixFileMode(path);
			return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
		}
		catch (Exception)
		{
			return false;
		}
	}
}

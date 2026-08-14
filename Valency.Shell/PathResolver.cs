namespace Valency.Shell;

public static class PathResolver
{
    public static string? Resolve(string command)
    {
        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            var full = Path.GetFullPath(command, Environment.CurrentDirectory);
            return File.Exists(full) ? full : null;
        }

        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        var hasExtension = !string.IsNullOrEmpty(Path.GetExtension(command));
        var candidates = hasExtension
            ? new[] { command }
            : pathExt.Select(ext => command + ext).Prepend(command);

        var searchDirs = new[] { Environment.CurrentDirectory }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

        foreach (var dir in searchDirs)
        {
            foreach (var candidate in candidates)
            {
                try
                {
                    var full = Path.Combine(dir, candidate);
                    if (File.Exists(full))
                        return full;
                }
                catch (ArgumentException)
                {
                }
            }
        }

        return null;
    }
}

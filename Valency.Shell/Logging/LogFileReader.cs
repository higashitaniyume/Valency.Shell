namespace Valency.Shell.Logging;

public readonly record struct LogLine(string Level, string Raw);

public static class LogFileReader
{
	public static List<LogLine> Read(string path)
	{
		var lines = new List<LogLine>();
		using var stream = new FileStream(
			path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		using var reader = new StreamReader(stream);
		while (reader.ReadLine() is { } raw)
			lines.Add(Parse(raw));
		return lines;
	}

	public static IReadOnlyList<LogLine> Filter(
		IReadOnlyList<LogLine> lines,
		string? level,
		int head,
		int tail)
	{
		IEnumerable<LogLine> query = lines;

		if (level is not null)
		{
			var threshold = LevelRank(level);
			query = query.Where(l => Rank(l.Level) >= threshold);
		}

		if (head >= 0)
			query = query.Take(head);
		else if (tail >= 0)
			query = query.TakeLast(tail);

		return query.ToList();
	}

	public static int LevelRank(string level)
	{
		return level.ToLowerInvariant() switch
		{
			"verbose" or "trace" or "vrb" => 0,
			"debug" => 1,
			"info" or "information" => 2,
			"warn" or "warning" => 3,
			"error" => 4,
			"fatal" or "critical" => 5,
			_ => -1,
		};
	}

	private static int Rank(string code)
	{
		return code switch
		{
			"VRB" => 0,
			"DBG" => 1,
			"INF" => 2,
			"WRN" => 3,
			"ERR" => 4,
			"FTL" => 5,
			_ => -1,
		};
	}

	private static LogLine Parse(string raw)
	{
		var level = string.Empty;
		var idx = raw.IndexOf('[');
		if (idx >= 0 && idx + 4 < raw.Length && raw[idx + 4] == ']')
			level = raw.Substring(idx + 1, 3);

		return new LogLine(level, raw);
	}
}

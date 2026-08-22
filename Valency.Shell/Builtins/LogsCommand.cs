using System.Diagnostics;
using Valency.Shell.Core.Builtins;
using Valency.Shell.Logging;

namespace Valency.Shell.Builtins;

public sealed class LogsCommand : IBuiltinCommand
{
	private readonly string _logFilePath;
	private readonly int _udpPort;

	public LogsCommand(string logFilePath, int udpPort)
	{
		_logFilePath = logFilePath;
		_udpPort = udpPort;
	}

	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Logs,
		Summary = Resources.LogsSummary,
		Options =
		[
			new("tail", 'n', Resources.LogsTail, false, "N"),
			new("head", null, Resources.LogsHead, false, "N"),
			new("level", null, Resources.LogsLevel, false, "LEVEL"),
			new("follow", 'f', Resources.LogsFollow, true),
		],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		if (args.Has("follow"))
			return Follow();

		var tail = args.GetInt("tail") ?? -1;
		var head = args.GetInt("head") ?? -1;
		var level = args.Get("level");
		return PrintRange(head, tail, level);
	}

	private int PrintRange(int head, int tail, string? level)
	{
		if (!File.Exists(_logFilePath))
		{
			Console.Error.WriteLine(string.Format(Resources.LogsFileNotFound, _logFilePath));
			return 1;
		}

		var lines = LogFileReader.Read(_logFilePath);
		var result = LogFileReader.Filter(lines, level, head, tail);

		foreach (var line in result)
			PrintColored(line.Raw);

		Console.Out.WriteLine(string.Format(Resources.LogsTotalLines, result.Count));
		return 0;
	}

	private int Follow()
	{
		var viewer = ResolveViewerPath();
		if (viewer is null)
		{
			Console.Out.WriteLine(Resources.LogsViewerNotFound);
			return 1;
		}

		if (OperatingSystem.IsWindows())
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "cmd",
				Arguments = $"/c start \"Valency Log\" \"{viewer}\" --udp {_udpPort}",
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			Process.Start(startInfo);
			Console.Out.WriteLine(Resources.LogsOpenedNewWindow);
		}
		else
		{
			Console.Out.WriteLine(string.Format(Resources.LogsRunInAnotherTerminal, viewer, _udpPort));
		}

		return 0;
	}

	private static string? ResolveViewerPath()
	{
		var candidate = Path.Combine(AppContext.BaseDirectory, "Valency.Shell.LogViewer.exe");
		return File.Exists(candidate) ? candidate : null;
	}

	private static void PrintColored(string raw)
	{
		if (Console.IsOutputRedirected)
		{
			Console.Out.WriteLine(raw);
			return;
		}

		var color = raw switch
		{
			_ when raw.Contains("[FTL]") => ConsoleColor.Magenta,
			_ when raw.Contains("[ERR]") => ConsoleColor.Red,
			_ when raw.Contains("[WRN]") => ConsoleColor.Yellow,
			_ when raw.Contains("[DBG]") || raw.Contains("[VRB]") => ConsoleColor.DarkGray,
			_ => ConsoleColor.Gray,
		};

		var previous = Console.ForegroundColor;
		Console.ForegroundColor = color;
		Console.Out.WriteLine(raw);
		Console.ForegroundColor = previous;
	}
}

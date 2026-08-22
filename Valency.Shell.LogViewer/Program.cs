using System.Net;
using System.Net.Sockets;
using System.Text;
using Valency.Shell.LogViewer;

const int defaultPort = 7310;

var mode = "udp";
var port = defaultPort;
string? file = null;
var tailLines = 0;

for (var i = 0; i < args.Length; i++)
{
	switch (args[i])
	{
		case "--udp":
			mode = "udp";
			if (i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
			{
				port = p;
				i++;
			}
			break;
		case "--file":
			mode = "file";
			if (i + 1 < args.Length)
			{
				file = args[i + 1];
				i++;
			}
			break;
		case "--tail":
			if (i + 1 < args.Length && int.TryParse(args[i + 1], out var n))
			{
				tailLines = n;
				i++;
			}
			break;
		case "--help":
			PrintUsage();
			return 0;
	}
}

if (mode == "file")
{
	if (file is null)
	{
		Console.Error.WriteLine(Resources.FileNeedPath);
		PrintUsage();
		return 2;
	}
	TailFile(file, tailLines);
}
else
{
	ListenUdp(port);
}

return 0;

static void PrintUsage()
{
	Console.Out.WriteLine(Resources.UsageHeader);
	Console.Out.WriteLine(Resources.UsageUdp);
	Console.Out.WriteLine(Resources.UsageFile);
	Console.Out.WriteLine(Resources.UsageTail);
}

static void ListenUdp(int port)
{
	using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
	Console.WriteLine(string.Format(Resources.ListeningUdp, port));
	while (true)
	{
		var remote = new IPEndPoint(IPAddress.Any, 0);
		var bytes = client.Receive(ref remote);
		PrintColorized(Encoding.UTF8.GetString(bytes));
	}
}

static void TailFile(string path, int tailLines)
{
	using var stream = new FileStream(
		path,
		FileMode.OpenOrCreate,
		FileAccess.Read,
		FileShare.ReadWrite | FileShare.Delete);

	var buffer = new byte[4096];
	long position;

	if (tailLines > 0)
	{
		var sb = new StringBuilder();
		position = Math.Max(0, stream.Length - 1);
		var lineCount = 0;
		while (position > 0 && lineCount <= tailLines)
		{
			stream.Seek(position--, SeekOrigin.Begin);
			var b = stream.ReadByte();
			if (b == '\n')
				lineCount++;
			if (lineCount <= tailLines)
				sb.Insert(0, (char)b);
		}
		Console.Out.Write(sb.ToString());
		position = stream.Length;
	}
	else
	{
		position = stream.Length;
	}

	Console.WriteLine(string.Format(Resources.FollowingFile, path));

	while (true)
	{
		var length = stream.Length;
		if (length < position)
		{
			stream.Seek(0, SeekOrigin.Begin);
			position = 0;
		}
		else if (length > position)
		{
			stream.Seek(position, SeekOrigin.Begin);
			var toRead = (int)Math.Min(buffer.Length, length - position);
			var read = stream.Read(buffer, 0, toRead);
			PrintColorized(Encoding.UTF8.GetString(buffer, 0, read));
			position += read;
		}
		else
		{
			Thread.Sleep(200);
		}
	}
}

static void PrintColorized(string text)
{
	foreach (var line in SplitLines(text))
		PrintLine(line);
}

static IEnumerable<string> SplitLines(string text)
{
	var start = 0;
	for (var i = 0; i < text.Length; i++)
	{
		if (text[i] == '\n')
		{
			yield return text[start..(i + 1)].TrimEnd('\r', '\n');
			start = i + 1;
		}
	}
	if (start < text.Length)
		yield return text[start..].TrimEnd('\r', '\n');
}

static void PrintLine(string line)
{
	if (Console.IsOutputRedirected)
	{
		Console.Out.WriteLine(line);
		return;
	}

	var color = line switch
	{
		_ when line.Contains("[FTL]") => ConsoleColor.Magenta,
		_ when line.Contains("[ERR]") => ConsoleColor.Red,
		_ when line.Contains("[WRN]") => ConsoleColor.Yellow,
		_ when line.Contains("[DBG]") || line.Contains("[VRB]") => ConsoleColor.DarkGray,
		_ => ConsoleColor.Gray,
	};

	var previous = Console.ForegroundColor;
	Console.ForegroundColor = color;
	Console.Out.WriteLine(line);
	Console.ForegroundColor = previous;
}

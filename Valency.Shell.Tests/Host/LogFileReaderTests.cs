using Valency.Shell.Logging;

namespace Valency.Shell.Tests.Host;

public class LogFileReaderTests
{
	private static LogLine L(string level, string raw) => new(level, raw);

	[Fact]
	public void Filter_Tail_ReturnsLastN()
	{
		var lines = new List<LogLine>
		{
			L("INF", "1"), L("INF", "2"), L("INF", "3"), L("INF", "4"),
		};
		var result = LogFileReader.Filter(lines, null, -1, 2);
		Assert.Equal(["3", "4"], result.Select(l => l.Raw).ToArray());
	}

	[Fact]
	public void Filter_Head_ReturnsFirstN()
	{
		var lines = new List<LogLine>
		{
			L("INF", "1"), L("INF", "2"), L("INF", "3"), L("INF", "4"),
		};
		var result = LogFileReader.Filter(lines, null, 2, -1);
		Assert.Equal(["1", "2"], result.Select(l => l.Raw).ToArray());
	}

	[Fact]
	public void Filter_Level_Error_ShowsErrorAndFatal()
	{
		var lines = new List<LogLine>
		{
			L("DBG", "dbg"), L("INF", "inf"), L("WRN", "wrn"), L("ERR", "err"), L("FTL", "ftl"),
		};
		var result = LogFileReader.Filter(lines, "error", -1, -1);
		Assert.Equal(["err", "ftl"], result.Select(l => l.Raw).ToArray());
	}

	[Fact]
	public void Filter_NoArgs_ReturnsAll()
	{
		var lines = new List<LogLine> { L("INF", "a"), L("WRN", "b") };
		var result = LogFileReader.Filter(lines, null, -1, -1);
		Assert.Equal(2, result.Count);
	}

	[Fact]
	public void Read_ParsesLevelFromBrackets()
	{
		var temp = Path.Combine(Path.GetTempPath(), "valency-log-" + Guid.NewGuid().ToString("N") + ".log");
		File.WriteAllLines(temp, ["10:00:00.000 [INF] hello", "10:00:01.000 [ERR] boom"]);
		try
		{
			var lines = LogFileReader.Read(temp);
			Assert.Equal(2, lines.Count);
			Assert.Equal("INF", lines[0].Level);
			Assert.Equal("ERR", lines[1].Level);
		}
		finally
		{
			File.Delete(temp);
		}
	}
}

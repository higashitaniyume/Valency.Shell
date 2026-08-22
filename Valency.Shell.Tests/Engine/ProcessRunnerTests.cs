namespace Valency.Shell.Tests.Engine;

public class ProcessRunnerTests
{
	[Fact]
	public void Run_UnknownCommand_ReturnsNonZero()
	{
		Assert.Equal(127, ProcessRunner.Run("definitely-not-a-command-xyz", []));
	}

	[Fact]
	public void Run_Exe_ReturnsChildExitCode()
	{
		Assert.Equal(42, ProcessRunner.Run("cmd", ["/c", "exit", "42"]));
	}

	[Fact]
	public void Run_BatchFile_RunsViaCmd()
	{
		var dir = Path.Combine(Path.GetTempPath(), "valency-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var bat = Path.Combine(dir, "t.bat");
			File.WriteAllText(bat, "@exit /b 7");
			Assert.Equal(7, ProcessRunner.Run(bat, []));
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	[Fact]
	public void RunPipeline_PassesDataBetweenCommands()
	{
		var code = ProcessRunner.RunPipeline(
		[
			["cmd", "/c", "echo hello"],
			["cmd", "/c", "findstr", "hello"],
		]);
		Assert.Equal(0, code);
	}

	[Fact]
	public void RunPipeline_FailureReturnsLastExitCode()
	{
		var code = ProcessRunner.RunPipeline(
		[
			["cmd", "/c", "echo hello"],
			["cmd", "/c", "findstr", "nomatch"],
		]);
		Assert.Equal(1, code);
	}
}

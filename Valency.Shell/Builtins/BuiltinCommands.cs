using Valency.Shell.Core.Builtins;
using Valency.Shell.Prompting;

namespace Valency.Shell.Builtins;

public static class BuiltinCommands
{
	public static BuiltinRegistry CreateDefault(string logFilePath, int udpPort, PromptSettings promptSettings)
	{
		var help = new HelpCommand();
		var builtins = new BuiltinRegistry(
			new ExitCommand(),
			new CdCommand(),
			new PwdCommand(),
			new JobsCommand(),
			new LogsCommand(logFilePath, udpPort),
			new PromptCommand(promptSettings),
			new GrepCommand(),
			help,
			new EchoCommand(),
			new TestCommand(bracket: false),
			new TestCommand(bracket: true),
			new TrueFalseColonCommand(BuiltinNames.True, 0),
			new TrueFalseColonCommand(BuiltinNames.False, 1),
			new TrueFalseColonCommand(BuiltinNames.Colon, 0),
			new ExportCommand(),
			new UnsetCommand(),
			new ReadCommand(),
			new ShiftCommand(),
			new SourceCommand(BuiltinNames.Source),
			new SourceCommand(BuiltinNames.Dot),
			new BreakCommand(),
			new ContinueCommand(),
			new ReturnCommand());
		help.Registry = builtins;
		return builtins;
	}
}

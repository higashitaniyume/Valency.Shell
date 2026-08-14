using Valency.Shell;
using Valency.Shell.Builtins;

Console.CancelKeyPress += (_, e) => e.Cancel = true;

var builtins = new BuiltinRegistry(
    new ExitCommand(),
    new CdCommand(),
    new PwdCommand(),
    new JobsCommand());

var shell = new Shell(builtins);
return shell.Run();

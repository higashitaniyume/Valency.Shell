using Valency.Shell;

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.Out.WriteLine();
};

var shell = new Shell();
return shell.Run();

using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class SourceCommand : IBuiltinCommand
{
    private readonly string _name;

    public CommandSpec Spec { get; }

    public SourceCommand(string name)
    {
        _name = name;
        Spec = new CommandSpec
        {
            Name = _name,
            Summary = "读取并执行脚本文件。",
            Positionals = ["FILE 脚本文件路径"],
        };
    }

    public int Execute(ParseResult args, IShellContext context)
    {
        if (args.Positionals.Count == 0)
        {
            Console.Error.WriteLine($"{_name}: 需要文件路径");
            return 2;
        }
        return context.RunScriptFile(args.Positionals[0]);
    }
}

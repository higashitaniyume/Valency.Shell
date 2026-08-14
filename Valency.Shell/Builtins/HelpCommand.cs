using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class HelpCommand : IBuiltinCommand
{
    private BuiltinRegistry? _registry;

    public BuiltinRegistry? Registry
    {
        get => _registry;
        set => _registry = value;
    }

    public CommandSpec Spec { get; } = new()
    {
        Name = BuiltinNames.Help,
        Summary = "显示内置命令的帮助。无参数列出所有命令。",
        Positionals = ["[command] 要查看详情的命令名"],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        if (_registry is null)
            return 1;

        if (args.Positionals.Count == 0)
        {
            Console.Out.WriteLine("内置命令:");
            foreach (var command in _registry.Commands)
                Console.Out.WriteLine($"  {command.Spec.Name,-12} {command.Spec.Summary}");
            return 0;
        }

        if (_registry.TryGet(args.Positionals[0], out var found))
        {
            HelpRenderer.PrintCommand(found.Spec);
            return 0;
        }

        Console.Error.WriteLine($"help: 未知命令 '{args.Positionals[0]}'");
        return 1;
    }
}

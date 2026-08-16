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
            Summary = Resources.SourceSummary,
            Positionals = [Resources.SourcePositional],
        };
    }

    public int Execute(ParseResult args, IShellContext context)
    {
        if (args.Positionals.Count == 0)
        {
            Console.Error.WriteLine(string.Format(Resources.SourceNeedFile, _name));
            return 2;
        }
        return context.RunScriptFile(args.Positionals[0]);
    }
}

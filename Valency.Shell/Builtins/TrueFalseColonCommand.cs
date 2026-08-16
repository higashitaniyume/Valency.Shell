using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class TrueFalseColonCommand : IBuiltinCommand
{
    private readonly string _name;
    private readonly int _code;

    public CommandSpec Spec { get; }

    public TrueFalseColonCommand(string name, int code)
    {
        _name = name;
        _code = code;
        Spec = new CommandSpec
        {
            Name = _name,
            Summary = _code == 0 ? Resources.TfAlwaysSucceed : Resources.TfAlwaysFail,
        };
    }

    public int Execute(ParseResult args, IShellContext context) => _code;
}

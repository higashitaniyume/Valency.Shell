using Valency.Shell.Core.Builtins;

namespace Valency.Shell.Builtins;

public sealed class TestCommand : IBuiltinCommand
{
    private readonly bool _bracket;

    public CommandSpec Spec { get; }

    public TestCommand(bool bracket)
    {
        _bracket = bracket;
        Spec = new CommandSpec
        {
            Name = _bracket ? BuiltinNames.Bracket : BuiltinNames.Test,
            Summary = _bracket
                ? Resources.TestSummaryBracket
                : Resources.TestSummary,
            Positionals = [Resources.TestPositional],
            RawArgs = true,
        };
    }

    public int Execute(ParseResult args, IShellContext context)
    {
        var operands = new List<string>(args.Positionals);

        if (_bracket)
        {
            if (operands.Count == 0 || operands[^1] != "]")
            {
                Console.Error.WriteLine(Resources.TestMissingBracket);
                return 2;
            }
            operands.RemoveAt(operands.Count - 1);
        }

        return Evaluate(operands) ? 0 : 1;
    }

    private static bool Evaluate(IReadOnlyList<string> operands)
    {
        if (operands.Count == 1)
            return operands[0].Length > 0;

        if (operands.Count == 2)
        {
            return operands[0] switch
            {
                "!" => operands[1].Length == 0,
                "-z" => operands[1].Length == 0,
                "-n" => operands[1].Length > 0,
                "-e" => File.Exists(operands[1]) || Directory.Exists(operands[1]),
                "-f" => File.Exists(operands[1]),
                "-d" => Directory.Exists(operands[1]),
                _ => false,
            };
        }

        if (operands.Count == 3)
        {
            var left = operands[0];
            var op = operands[1];
            var right = operands[2];

            switch (op)
            {
                case "=":
                case "==":
                    return string.Equals(left, right, StringComparison.Ordinal);
                case "!=":
                    return !string.Equals(left, right, StringComparison.Ordinal);
                case "-eq":
                    return CompareInt(left, right, (a, b) => a == b);
                case "-ne":
                    return CompareInt(left, right, (a, b) => a != b);
                case "-lt":
                    return CompareInt(left, right, (a, b) => a < b);
                case "-le":
                    return CompareInt(left, right, (a, b) => a <= b);
                case "-gt":
                    return CompareInt(left, right, (a, b) => a > b);
                case "-ge":
                    return CompareInt(left, right, (a, b) => a >= b);
            }
        }

        if (operands.Count == 3 && operands[0] == "!")
            return !Evaluate([operands[1], operands[2]]);

        return false;
    }

    private static bool CompareInt(string left, string right, Func<long, long, bool> compare)
    {
        return long.TryParse(left, out var a) && long.TryParse(right, out var b) && compare(a, b);
    }
}

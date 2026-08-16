using System.Text;
using Valency.Shell.Core.Expansion;
using Valency.Shell.Scripting.Arithmetic;
using Valency.Shell.Scripting.Ast;

namespace Valency.Shell.Scripting.Expansion;

public readonly record struct ExpandedWord(string Text, bool Glob);

public sealed class WordExpander
{
    private readonly VariableExpander _variableExpander;
    private readonly Func<string, string> _commandSubstitution;
    private readonly Func<string, long> _arithmeticResolver;

    public WordExpander(
        IVariableSource variables,
        Func<string, string> commandSubstitution,
        Func<string, long> arithmeticResolver)
    {
        _variableExpander = new VariableExpander(variables);
        _commandSubstitution = commandSubstitution;
        _arithmeticResolver = arithmeticResolver;
    }

    public ExpandedWord Expand(Word word)
    {
        var sb = new StringBuilder();
        var anyUnquoted = false;
        var first = true;

        foreach (var part in word.Parts)
        {
            switch (part)
            {
                case LiteralPart { Quoted: var quoted } lit:
                {
                    var literalText = first && !quoted
                        ? VariableExpander.ExpandTilde(lit.Text, expandable: true)
                        : lit.Text;
                    if (!quoted)
                        anyUnquoted = true;
                    sb.Append(_variableExpander.ExpandText(literalText));
                    break;
                }
                case SingleQuotedPart sq:
                    sb.Append(sq.Text);
                    break;
                case CommandSubPart cs:
                    anyUnquoted = true;
                    sb.Append(_commandSubstitution(cs.Command));
                    break;
                case ArithSubPart ar:
                    anyUnquoted = true;
                    sb.Append(ArithmeticEvaluator.Evaluate(ar.Expression, _arithmeticResolver).ToString());
                    break;
            }
            first = false;
        }

        var text = sb.ToString();
        var glob = anyUnquoted && GlobExpander.HasGlob(text);
        return new ExpandedWord(text, glob);
    }

    public string ExpandToString(Word word) => Expand(word).Text;
}

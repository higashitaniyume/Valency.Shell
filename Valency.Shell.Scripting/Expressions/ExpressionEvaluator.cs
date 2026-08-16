using System.Globalization;
using System.Text;
using Serilog;

namespace Valency.Shell.Scripting.Expressions;

public static class ExpressionEvaluator
{
    public static Value Evaluate(
        string expression,
        Func<string, Value> getVariable,
        Action<string, Value>? setVariable = null,
        Func<string, string>? commandSubstitution = null,
        ILogger? logger = null)
    {
        var value = new Impl(expression, getVariable, setVariable, commandSubstitution).ParseExpression();
        logger?.ForContext("Src", "expr")
            .Debug(Resources.LogExpressionEvaluated, expression, value.AsString());
        return value;
    }

    private sealed class Impl
    {
        private readonly string _expr;
        private readonly Func<string, Value> _get;
        private readonly Action<string, Value>? _set;
        private readonly Func<string, string>? _commandSub;
        private int _i;
        private string _lastIdentifier = string.Empty;

        public Impl(string expression, Func<string, Value> get, Action<string, Value>? set, Func<string, string>? commandSub)
        {
            _expr = expression;
            _get = get;
            _set = set;
            _commandSub = commandSub;
        }

        private bool AtEnd => _i >= _expr.Length;

        public Value ParseExpression()
        {
            var value = ParseAssignment();
            SkipWs();
            while (Peek(','))
            {
                Advance();
                SkipWs();
                value = ParseAssignment();
                SkipWs();
            }
            return value;
        }

        private Value ParseAssignment()
        {
            SkipWs();
            if (AtEnd)
                throw Error(Resources.ExpressionEmpty);

            var save = _i;
            if (TryReadDollarIdentifier(out var name))
            {
                SkipWs();
                var op = ReadCompoundAssignmentOp();
                if (op is not null)
                {
                    var rhs = ParseAssignment();
                    return Assign(name, op, rhs);
                }
                _i = save;
            }

            return ParseConditional();
        }

        private Value ParseConditional()
        {
            var cond = ParseLogicalOr();
            SkipWs();
            if (Peek('?'))
            {
                Advance();
                var whenTrue = ParseExpression();
                SkipWs();
                if (!Peek(':'))
                    throw Error(Resources.TernaryMissingColon);
                Advance();
                var whenFalse = ParseConditional();
                return cond.Truthy ? whenTrue : whenFalse;
            }
            return cond;
        }

        private Value ParseLogicalOr()
        {
            var left = ParseLogicalAnd();
            SkipWs();
            while (Peek('|') && Peek('|', 1))
            {
                Advance();
                Advance();
                var right = ParseLogicalAnd();
                left = Value.Bool(left.Truthy || right.Truthy);
                SkipWs();
            }
            return left;
        }

        private Value ParseLogicalAnd()
        {
            var left = ParseBitOr();
            SkipWs();
            while (Peek('&') && Peek('&', 1))
            {
                Advance();
                Advance();
                var right = ParseBitOr();
                left = Value.Bool(left.Truthy && right.Truthy);
                SkipWs();
            }
            return left;
        }

        private Value ParseBitOr()
        {
            var left = ParseBitXor();
            SkipWs();
            while (Peek('|') && !Peek('|', 1))
            {
                Advance();
                left = Value.Int(left.AsInt() | ParseBitXor().AsInt());
                SkipWs();
            }
            return left;
        }

        private Value ParseBitXor()
        {
            var left = ParseBitAnd();
            SkipWs();
            while (Peek('^'))
            {
                Advance();
                left = Value.Int(left.AsInt() ^ ParseBitAnd().AsInt());
                SkipWs();
            }
            return left;
        }

        private Value ParseBitAnd()
        {
            var left = ParseEquality();
            SkipWs();
            while (Peek('&') && !Peek('&', 1))
            {
                Advance();
                left = Value.Int(left.AsInt() & ParseEquality().AsInt());
                SkipWs();
            }
            return left;
        }

        private Value ParseEquality()
        {
            var left = ParseRelational();
            SkipWs();
            while (true)
            {
                if (Peek('=') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = Value.Bool(Equals(left, ParseRelational()));
                }
                else if (Peek('!') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = Value.Bool(!Equals(left, ParseRelational()));
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private Value ParseRelational()
        {
            var left = ParseShift();
            SkipWs();
            while (true)
            {
                if (Peek('<') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = Value.Bool(Compare(left, ParseShift()) <= 0);
                }
                else if (Peek('>') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = Value.Bool(Compare(left, ParseShift()) >= 0);
                }
                else if (Peek('<'))
                {
                    Advance();
                    left = Value.Bool(Compare(left, ParseShift()) < 0);
                }
                else if (Peek('>'))
                {
                    Advance();
                    left = Value.Bool(Compare(left, ParseShift()) > 0);
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private Value ParseShift()
        {
            var left = ParseAdditive();
            SkipWs();
            while (true)
            {
                if (Peek('<') && Peek('<', 1))
                {
                    Advance();
                    Advance();
                    left = Value.Int(left.AsInt() << (int)ParseAdditive().AsInt());
                }
                else if (Peek('>') && Peek('>', 1))
                {
                    Advance();
                    Advance();
                    left = Value.Int(left.AsInt() >> (int)ParseAdditive().AsInt());
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private Value ParseAdditive()
        {
            var left = ParseMultiplicative();
            SkipWs();
            while (true)
            {
                if (Peek('+') && !Peek('+', 1))
                {
                    Advance();
                    var right = ParseMultiplicative();
                    left = left.Kind == ValueKind.Str || right.Kind == ValueKind.Str
                        ? Value.Str(left.AsString() + right.AsString())
                        : Value.Int(left.AsInt() + right.AsInt());
                }
                else if (Peek('-') && !Peek('-', 1))
                {
                    Advance();
                    left = Value.Int(left.AsInt() - ParseMultiplicative().AsInt());
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private Value ParseMultiplicative()
        {
            var left = ParseUnary();
            SkipWs();
            while (true)
            {
                if (Peek('*') && !Peek('*', 1))
                {
                    Advance();
                    left = Value.Int(left.AsInt() * ParseUnary().AsInt());
                }
                else if (Peek('/'))
                {
                    Advance();
                    left = Value.Int(left.AsInt() / ParseUnary().AsInt());
                }
                else if (Peek('%'))
                {
                    Advance();
                    left = Value.Int(left.AsInt() % ParseUnary().AsInt());
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private Value ParseUnary()
        {
            SkipWs();
            if (Peek('+') && !Peek('+', 1))
            {
                Advance();
                return Value.Int(+ParseUnary().AsInt());
            }
            if (Peek('-') && !Peek('-', 1))
            {
                Advance();
                return Value.Int(-ParseUnary().AsInt());
            }
            if (Peek('!'))
            {
                Advance();
                return Value.Bool(!ParseUnary().Truthy);
            }
            if (Peek('~'))
            {
                Advance();
                return Value.Int(~ParseUnary().AsInt());
            }
            if (Peek('+') && Peek('+', 1))
            {
                Advance();
                Advance();
                SkipWs();
                var name = ReadDollarIdentifier();
                var value = Value.Int(_get(name).AsInt() + 1);
                _set?.Invoke(name, value);
                return value;
            }
            if (Peek('-') && Peek('-', 1))
            {
                Advance();
                Advance();
                SkipWs();
                var name = ReadDollarIdentifier();
                var value = Value.Int(_get(name).AsInt() - 1);
                _set?.Invoke(name, value);
                return value;
            }

            return ParsePostfix();
        }

        private Value ParsePostfix()
        {
            var value = ParsePrimary();
            SkipWs();
            if (Peek('+') && Peek('+', 1))
            {
                Advance();
                Advance();
                var updated = Value.Int(value.AsInt() + 1);
                _set?.Invoke(_lastIdentifier, updated);
                return value;
            }
            if (Peek('-') && Peek('-', 1))
            {
                Advance();
                Advance();
                var updated = Value.Int(value.AsInt() - 1);
                _set?.Invoke(_lastIdentifier, updated);
                return value;
            }
            return value;
        }

        private Value ParsePrimary()
        {
            SkipWs();
            if (AtEnd)
                throw Error(Resources.ExpressionUnexpectedEnd);

            var c = _expr[_i];

            if (char.IsDigit(c))
                return Value.Int(ReadNumber());

            if (c is '\'' or '"')
                return Value.Str(ReadString());

            if (c == '$')
            {
                if (_i + 1 < _expr.Length && _expr[_i + 1] == '(')
                    return Value.Str(ReadCommandSubstitution());

                var name = ReadDollarIdentifier();
                _lastIdentifier = name;
                return _get(name);
            }

            if (c == '(')
            {
                Advance();
                var value = ParseExpression();
                SkipWs();
                if (!Peek(')'))
                    throw Error(Resources.MissingCloseParen);
                Advance();
                _lastIdentifier = string.Empty;
                return value;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var word = ReadIdentifier();
                return word switch
                {
                    "true" => Value.Bool(true),
                    "false" => Value.Bool(false),
                    _ => throw Error(string.Format(Resources.UnknownIdentifier, word)),
                };
            }

            throw Error(string.Format(Resources.CannotParseExpressionChar, c));
        }

        private long ReadNumber()
        {
            var start = _i;
            if (Peek('0') && _i + 1 < _expr.Length && _expr[_i + 1] is 'x' or 'X')
            {
                _i += 2;
                var hexStart = _i;
                while (_i < _expr.Length && Uri.IsHexDigit(_expr[_i]))
                    _i++;
                return long.Parse(_expr[hexStart.._i], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            while (_i < _expr.Length && char.IsDigit(_expr[_i]))
                _i++;
            return long.Parse(_expr[start.._i], CultureInfo.InvariantCulture);
        }

        private string ReadString()
        {
            var quote = _expr[_i];
            Advance();
            var sb = new StringBuilder();
            while (_i < _expr.Length && _expr[_i] != quote)
            {
                if (quote == '"' && _expr[_i] == '\\' && _i + 1 < _expr.Length &&
                    _expr[_i + 1] is '"' or '\\' or 'n' or 't')
                {
                    sb.Append(_expr[_i + 1] switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        var ch => ch,
                    });
                    _i += 2;
                    continue;
                }
                sb.Append(_expr[_i]);
                Advance();
            }
            if (_i >= _expr.Length)
                throw Error(Resources.UnclosedString);
            Advance();
            return sb.ToString();
        }

        private string ReadCommandSubstitution()
        {
            Advance();
            Advance();
            var depth = 1;
            var sb = new StringBuilder();
            while (_i < _expr.Length)
            {
                var c = _expr[_i];
                if (c == '(')
                {
                    depth++;
                    sb.Append(c);
                    Advance();
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        Advance();
                        return _commandSub?.Invoke(sb.ToString()) ?? string.Empty;
                    }
                    sb.Append(c);
                    Advance();
                }
                else
                {
                    sb.Append(c);
                    Advance();
                }
            }
            throw Error(Resources.UnclosedCommandSubstitution);
        }

        private string ReadIdentifier()
        {
            SkipWs();
            var start = _i;
            while (_i < _expr.Length && (char.IsLetterOrDigit(_expr[_i]) || _expr[_i] == '_'))
                _i++;
            if (_i == start)
                throw Error(Resources.ExpectedIdentifier);
            return _expr[start.._i];
        }

        private string ReadDollarIdentifier()
        {
            SkipWs();
            if (_i >= _expr.Length || _expr[_i] != '$')
                throw Error(Resources.VariableNeedsDollar);
            Advance();

            if (_i < _expr.Length && _expr[_i] == '{')
            {
                Advance();
                var start = _i;
                while (_i < _expr.Length && _expr[_i] != '}')
                    Advance();
                if (_i >= _expr.Length)
                    throw Error(Resources.UnclosedBracedVariable);
                var name = _expr[start.._i];
                Advance();
                return name;
            }

            var nameStart = _i;
            while (_i < _expr.Length && (char.IsLetterOrDigit(_expr[_i]) || _expr[_i] == '_'))
                Advance();
            if (_i == nameStart)
                throw Error(Resources.DollarNeedsName);
            return _expr[nameStart.._i];
        }

        private bool TryReadDollarIdentifier(out string name)
        {
            var save = _i;
            SkipWs();
            if (_i < _expr.Length && _expr[_i] == '$')
            {
                try
                {
                    name = ReadDollarIdentifier();
                    return true;
                }
                catch (InvalidOperationException)
                {
                }
            }
            _i = save;
            name = string.Empty;
            return false;
        }

        private string? ReadCompoundAssignmentOp()
        {
            if (Peek('=') && !Peek('=', 1))
            {
                Advance();
                return "=";
            }
            var two = PeekTwo();
            if (two is "+=" or "-=" or "*=" or "/=" or "%=" or "&=" or "^=" or "|=")
            {
                Advance();
                Advance();
                return two;
            }
            if (Peek('<') && Peek('<', 1) && Peek('=', 2))
            {
                Advance();
                Advance();
                Advance();
                return "<<=";
            }
            if (Peek('>') && Peek('>', 1) && Peek('=', 2))
            {
                Advance();
                Advance();
                Advance();
                return ">>=";
            }
            return null;
        }

        private Value Assign(string name, string op, Value rhs)
        {
            var current = _get(name);
            var result = op switch
            {
                "=" => rhs,
                "+=" => current.Kind == ValueKind.Str || rhs.Kind == ValueKind.Str
                    ? Value.Str(current.AsString() + rhs.AsString())
                    : Value.Int(current.AsInt() + rhs.AsInt()),
                "-=" => Value.Int(current.AsInt() - rhs.AsInt()),
                "*=" => Value.Int(current.AsInt() * rhs.AsInt()),
                "/=" => Value.Int(current.AsInt() / rhs.AsInt()),
                "%=" => Value.Int(current.AsInt() % rhs.AsInt()),
                "&=" => Value.Int(current.AsInt() & rhs.AsInt()),
                "^=" => Value.Int(current.AsInt() ^ rhs.AsInt()),
                "|=" => Value.Int(current.AsInt() | rhs.AsInt()),
                "<<=" => Value.Int(current.AsInt() << (int)rhs.AsInt()),
                ">>=" => Value.Int(current.AsInt() >> (int)rhs.AsInt()),
                _ => rhs,
            };
            _set?.Invoke(name, result);
            return result;
        }

        private static bool Equals(Value left, Value right)
        {
            if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Int)
                return left.AsInt() == right.AsInt();
            return string.Equals(left.AsString(), right.AsString(), StringComparison.Ordinal);
        }

        private static int Compare(Value left, Value right)
        {
            if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Int)
                return left.AsInt().CompareTo(right.AsInt());
            return string.Compare(left.AsString(), right.AsString(), StringComparison.Ordinal);
        }

        private string? PeekTwo()
        {
            if (_i + 1 >= _expr.Length)
                return null;
            return _expr.Substring(_i, 2);
        }

        private bool Peek(char c, int offset = 0)
        {
            return _i + offset < _expr.Length && _expr[_i + offset] == c;
        }

        private void Advance() => _i++;

        private void SkipWs()
        {
            while (_i < _expr.Length && char.IsWhiteSpace(_expr[_i]))
                _i++;
        }

        private static InvalidOperationException Error(string message) => new(message);
    }
}

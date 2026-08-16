using System.Globalization;

namespace Valency.Shell.Scripting.Arithmetic;

public static class ArithmeticEvaluator
{
    public static long Evaluate(
        string expression,
        Func<string, long> getVariable,
        Action<string, long>? setVariable = null)
    {
        return new Impl(expression, getVariable, setVariable).ParseExpression();
    }

    private sealed class Impl
    {
        private readonly string _expr;
        private readonly Func<string, long> _get;
        private readonly Action<string, long>? _set;
        private int _i;

        public Impl(string expression, Func<string, long> get, Action<string, long>? set)
        {
            _expr = expression;
            _get = get;
            _set = set;
        }

        private bool AtEnd => _i >= _expr.Length;

        public long ParseExpression()
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

        private long ParseAssignment()
        {
            SkipWs();
            if (AtEnd)
                throw Error("算术表达式为空");

            var save = _i;
            if (TryReadIdentifier(out var name))
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

        private long ParseConditional()
        {
            var cond = ParseLogicalOr();
            SkipWs();
            if (Peek('?'))
            {
                Advance();
                var whenTrue = ParseExpression();
                SkipWs();
                if (!Peek(':'))
                    throw Error("三元表达式缺少 ':'");
                Advance();
                var whenFalse = ParseConditional();
                return cond != 0 ? whenTrue : whenFalse;
            }
            return cond;
        }

        private long ParseLogicalOr()
        {
            var left = ParseLogicalAnd();
            SkipWs();
            while (Peek('|') && Peek('|', 1))
            {
                Advance();
                Advance();
                var right = ParseLogicalAnd();
                left = left != 0 || right != 0 ? 1 : 0;
                SkipWs();
            }
            return left;
        }

        private long ParseLogicalAnd()
        {
            var left = ParseBitOr();
            SkipWs();
            while (Peek('&') && Peek('&', 1))
            {
                Advance();
                Advance();
                var right = ParseBitOr();
                left = left != 0 && right != 0 ? 1 : 0;
                SkipWs();
            }
            return left;
        }

        private long ParseBitOr()
        {
            var left = ParseBitXor();
            SkipWs();
            while (Peek('|') && !Peek('|', 1))
            {
                Advance();
                var right = ParseBitXor();
                left |= right;
                SkipWs();
            }
            return left;
        }

        private long ParseBitXor()
        {
            var left = ParseBitAnd();
            SkipWs();
            while (Peek('^'))
            {
                Advance();
                var right = ParseBitAnd();
                left ^= right;
                SkipWs();
            }
            return left;
        }

        private long ParseBitAnd()
        {
            var left = ParseEquality();
            SkipWs();
            while (Peek('&') && !Peek('&', 1))
            {
                Advance();
                var right = ParseEquality();
                left &= right;
                SkipWs();
            }
            return left;
        }

        private long ParseEquality()
        {
            var left = ParseRelational();
            SkipWs();
            while (true)
            {
                if (Peek('=') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = left == ParseRelational() ? 1 : 0;
                }
                else if (Peek('!') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = left != ParseRelational() ? 1 : 0;
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private long ParseRelational()
        {
            var left = ParseShift();
            SkipWs();
            while (true)
            {
                if (Peek('<') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = left <= ParseShift() ? 1 : 0;
                }
                else if (Peek('>') && Peek('=', 1))
                {
                    Advance();
                    Advance();
                    left = left >= ParseShift() ? 1 : 0;
                }
                else if (Peek('<'))
                {
                    Advance();
                    left = left < ParseShift() ? 1 : 0;
                }
                else if (Peek('>'))
                {
                    Advance();
                    left = left > ParseShift() ? 1 : 0;
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private long ParseShift()
        {
            var left = ParseAdditive();
            SkipWs();
            while (true)
            {
                if (Peek('<') && Peek('<', 1))
                {
                    Advance();
                    Advance();
                    left <<= (int)ParseAdditive();
                }
                else if (Peek('>') && Peek('>', 1))
                {
                    Advance();
                    Advance();
                    left >>= (int)ParseAdditive();
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private long ParseAdditive()
        {
            var left = ParseMultiplicative();
            SkipWs();
            while (true)
            {
                if (Peek('+') && !Peek('+', 1))
                {
                    Advance();
                    left += ParseMultiplicative();
                }
                else if (Peek('-') && !Peek('-', 1))
                {
                    Advance();
                    left -= ParseMultiplicative();
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private long ParseMultiplicative()
        {
            var left = ParseUnary();
            SkipWs();
            while (true)
            {
                if (Peek('*') && !Peek('*', 1))
                {
                    Advance();
                    left *= ParseUnary();
                }
                else if (Peek('/'))
                {
                    Advance();
                    left /= ParseUnary();
                }
                else if (Peek('%'))
                {
                    Advance();
                    left %= ParseUnary();
                }
                else
                {
                    break;
                }
                SkipWs();
            }
            return left;
        }

        private long ParseUnary()
        {
            SkipWs();
            if (Peek('+') && !Peek('+', 1))
            {
                Advance();
                return ParseUnary();
            }
            if (Peek('-') && !Peek('-', 1))
            {
                Advance();
                return -ParseUnary();
            }
            if (Peek('!'))
            {
                Advance();
                return ParseUnary() == 0 ? 1 : 0;
            }
            if (Peek('~'))
            {
                Advance();
                return ~ParseUnary();
            }
            if (Peek('+') && Peek('+', 1))
            {
                Advance();
                Advance();
                SkipWs();
                var name = ReadIdentifier();
                var value = _get(name) + 1;
                _set?.Invoke(name, value);
                return value;
            }
            if (Peek('-') && Peek('-', 1))
            {
                Advance();
                Advance();
                SkipWs();
                var name = ReadIdentifier();
                var value = _get(name) - 1;
                _set?.Invoke(name, value);
                return value;
            }

            return ParsePostfix();
        }

        private long ParsePostfix()
        {
            var value = ParsePrimary();
            SkipWs();
            if (Peek('+') && Peek('+', 1))
            {
                Advance();
                Advance();
                var name = ReadLastIdentifier();
                var updated = value + 1;
                _set?.Invoke(name, updated);
                return value;
            }
            if (Peek('-') && Peek('-', 1))
            {
                Advance();
                Advance();
                var name = ReadLastIdentifier();
                var updated = value - 1;
                _set?.Invoke(name, updated);
                return value;
            }
            return value;
        }

        private string _lastIdentifier = string.Empty;

        private long ParsePrimary()
        {
            SkipWs();
            if (AtEnd)
                throw Error("表达式意外结束");

            var c = _expr[_i];
            if (char.IsDigit(c))
                return ReadNumber();

            if (c is '(')
            {
                Advance();
                var value = ParseExpression();
                SkipWs();
                if (!Peek(')'))
                    throw Error("缺少 ')'");
                Advance();
                _lastIdentifier = string.Empty;
                return value;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var name = ReadIdentifier();
                _lastIdentifier = name;
                return _get(name);
            }

            throw Error($"无法解析算术符号 '{c}'");
        }

        private long ReadNumber()
        {
            var start = _i;
            if (Peek('0') && (_i + 1 < _expr.Length) &&
                (_expr[_i + 1] is 'x' or 'X'))
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

        private string ReadIdentifier()
        {
            SkipWs();
            var start = _i;
            while (_i < _expr.Length && (char.IsLetterOrDigit(_expr[_i]) || _expr[_i] == '_'))
                _i++;
            if (_i == start)
                throw Error("需要变量名");
            return _expr[start.._i];
        }

        private string ReadLastIdentifier() => _lastIdentifier;

        private bool TryReadIdentifier(out string name)
        {
            var save = _i;
            SkipWs();
            if (_i < _expr.Length && (char.IsLetter(_expr[_i]) || _expr[_i] == '_'))
            {
                var start = _i;
                while (_i < _expr.Length && (char.IsLetterOrDigit(_expr[_i]) || _expr[_i] == '_'))
                    _i++;
                name = _expr[start.._i];
                return true;
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

        private long Assign(string name, string op, long rhs)
        {
            var current = _get(name);
            var result = op switch
            {
                "=" => rhs,
                "+=" => current + rhs,
                "-=" => current - rhs,
                "*=" => current * rhs,
                "/=" => current / rhs,
                "%=" => current % rhs,
                "&=" => current & rhs,
                "^=" => current ^ rhs,
                "|=" => current | rhs,
                "<<=" => current << (int)rhs,
                ">>=" => current >> (int)rhs,
                _ => rhs,
            };
            _set?.Invoke(name, result);
            return result;
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

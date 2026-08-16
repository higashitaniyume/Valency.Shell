using System.Text;
using Valency.Shell.Scripting.Ast;

namespace Valency.Shell.Scripting.Lexing;

public sealed class SyntaxError : Exception
{
    public int Line { get; }
    public int Column { get; }

    public SyntaxError(string message, int line, int column)
        : base($"{message}（第 {line} 行，第 {column} 列）")
    {
        Line = line;
        Column = column;
    }
}

public static class ShellLexer
{
    public static IReadOnlyList<Token> Tokenize(string input) => new Lexer(input).Run();

    private sealed class Lexer
    {
        private readonly string _input;
        private int _i;
        private int _line = 1;
        private int _col = 1;
        private readonly List<Token> _tokens = new();
        private readonly List<WordPart> _parts = new();
        private readonly StringBuilder _sb = new();
        private bool _inDoubleQuote;
        private bool _hasWord;

        public Lexer(string input) => _input = input;

        public IReadOnlyList<Token> Run()
        {
            while (_i < _input.Length)
            {
                var c = _input[_i];

                if (c == '\r')
                {
                    Advance();
                    continue;
                }

                if (c == '\n')
                {
                    EmitWord();
                    Emit(TokenType.Newline, "\n");
                    Advance();
                    continue;
                }

                if (_inDoubleQuote)
                {
                    ScanDoubleQuoted(c);
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    EmitWord();
                    Advance();
                    continue;
                }

                switch (c)
                {
                    case '\'':
                        Advance();
                        ScanSingleQuote();
                        break;
                    case '"':
                        Advance();
                        FlushLiteral();
                        _inDoubleQuote = true;
                        _hasWord = true;
                        break;
                    case '$':
                        HandleDollar();
                        break;
                    case '`':
                        ScanBacktick();
                        break;
                    case '#':
                        if (AtWordStart())
                            SkipToLineEnd();
                        else
                            AppendLiteralAndAdvance(c);
                        break;
                    case ';':
                    case '|':
                    case '&':
                    case '<':
                    case '>':
                        HandleOperator();
                        break;
                    case '(':
                        HandleLParen();
                        break;
                    case ')':
                        HandleRParen();
                        break;
                    case '{':
                        HandleLBrace();
                        break;
                    case '}':
                        HandleRBrace();
                        break;
                    case '!':
                        if (AtWordStart())
                        {
                            EmitSimple(TokenType.Bang, "!");
                            Advance();
                        }
                        else
                        {
                            AppendLiteralAndAdvance(c);
                        }
                        break;
                    default:
                        AppendLiteralAndAdvance(c);
                        break;
                }
            }

            EmitWord();
            if (_inDoubleQuote)
                throw new SyntaxError("双引号未闭合", _line, _col);
            Emit(TokenType.EndOfFile, "");
            return _tokens;
        }

        private void ScanDoubleQuoted(char c)
        {
            if (c == '"')
            {
                Advance();
                FlushLiteral();
                _inDoubleQuote = false;
                return;
            }

            if (c == '\\' && _i + 1 < _input.Length && _input[_i + 1] is '"' or '\\' or '$' or '`')
            {
                Advance();
                AppendLiteral(_input[_i]);
                Advance();
                return;
            }

            if (c == '$')
            {
                HandleDollar();
                return;
            }

            if (c == '`')
            {
                ScanBacktick();
                return;
            }

            AppendLiteralAndAdvance(c);
        }

        private void HandleDollar()
        {
            Advance();
            if (_i < _input.Length && _input[_i] == '(')
            {
                if (_i + 1 < _input.Length && _input[_i + 1] == '(')
                    ScanArithSub();
                else
                    ScanCommandSub();
                return;
            }

            AppendLiteral('$');
        }

        private void ScanCommandSub()
        {
            FlushLiteral();
            Advance();
            var depth = 1;
            var sb = new StringBuilder();
            while (_i < _input.Length)
            {
                var c = _input[_i];
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
                        _parts.Add(new CommandSubPart(sb.ToString()));
                        _hasWord = true;
                        return;
                    }
                    sb.Append(c);
                    Advance();
                }
                else if (c is '\'' or '"')
                {
                    var quote = c;
                    sb.Append(c);
                    Advance();
                    while (_i < _input.Length && _input[_i] != quote)
                    {
                        sb.Append(_input[_i]);
                        Advance();
                    }
                    if (_i < _input.Length)
                    {
                        sb.Append(_input[_i]);
                        Advance();
                    }
                }
                else
                {
                    sb.Append(c);
                    Advance();
                }
            }
            throw new SyntaxError("命令替换 $(...) 未闭合", _line, _col);
        }

        private void ScanArithSub()
        {
            FlushLiteral();
            Advance();
            Advance();
            var sb = new StringBuilder();
            while (_i < _input.Length)
            {
                if (_input[_i] == ')' && _i + 1 < _input.Length && _input[_i + 1] == ')')
                {
                    Advance();
                    Advance();
                    _parts.Add(new ArithSubPart(sb.ToString()));
                    _hasWord = true;
                    return;
                }
                sb.Append(_input[_i]);
                Advance();
            }
            throw new SyntaxError("算术展开 $((...)) 未闭合", _line, _col);
        }

        private void ScanBacktick()
        {
            FlushLiteral();
            Advance();
            var sb = new StringBuilder();
            while (_i < _input.Length && _input[_i] != '`')
            {
                sb.Append(_input[_i]);
                Advance();
            }
            if (_i >= _input.Length)
                throw new SyntaxError("反引号未闭合", _line, _col);
            Advance();
            _parts.Add(new CommandSubPart(sb.ToString()));
            _hasWord = true;
        }

        private void ScanSingleQuote()
        {
            FlushLiteral();
            var sb = new StringBuilder();
            while (_i < _input.Length && _input[_i] != '\'')
            {
                sb.Append(_input[_i]);
                Advance();
            }
            if (_i >= _input.Length)
                throw new SyntaxError("单引号未闭合", _line, _col);
            Advance();
            _parts.Add(new SingleQuotedPart(sb.ToString()));
            _hasWord = true;
        }

        private void HandleOperator()
        {
            var c = _input[_i];
            switch (c)
            {
                case ';':
                    EmitWord();
                    EmitSimple(TokenType.Semi, ";");
                    break;

                case '&':
                    EmitWord();
                    if (_i + 1 < _input.Length && _input[_i + 1] == '&')
                    {
                        EmitSimple(TokenType.AndIf, "&&");
                        Advance();
                    }
                    else if (_i + 1 < _input.Length && _input[_i + 1] == '>')
                    {
                        if (_i + 2 < _input.Length && _input[_i + 2] == '>')
                        {
                            EmitSimple(TokenType.AndGreatAnd, "&>>");
                            Advance();
                            Advance();
                        }
                        else
                        {
                            EmitSimple(TokenType.AndGreat, "&>");
                            Advance();
                        }
                    }
                    else
                    {
                        EmitSimple(TokenType.Background, "&");
                    }
                    break;

                case '|':
                    EmitWord();
                    if (_i + 1 < _input.Length && _input[_i + 1] == '|')
                    {
                        EmitSimple(TokenType.OrIf, "||");
                        Advance();
                    }
                    else
                    {
                        EmitSimple(TokenType.Pipe, "|");
                    }
                    break;

                case '<':
                case '>':
                    HandleRedirect(c);
                    break;
            }
            Advance();
        }

        private void HandleRedirect(char c)
        {
            string fd = string.Empty;
            if (_sb.Length > 0 && AllDigits(_sb))
            {
                fd = _sb.ToString();
                _sb.Clear();
                _hasWord = false;
            }
            else
            {
                EmitWord();
            }

            if (c == '<')
            {
                if (_i + 1 < _input.Length && _input[_i + 1] == '<')
                {
                    if (_i + 2 < _input.Length && _input[_i + 2] == '-')
                    {
                        EmitSimple(TokenType.DLessDash, fd + "<<-");
                        Advance();
                        Advance();
                    }
                    else
                    {
                        EmitSimple(TokenType.DLess, fd + "<<");
                        Advance();
                    }
                }
                else if (_i + 1 < _input.Length && _input[_i + 1] == '&')
                {
                    EmitSimple(TokenType.LessAnd, fd + "<&");
                    Advance();
                }
                else if (_i + 1 < _input.Length && _input[_i + 1] == '>')
                {
                    EmitSimple(TokenType.LessGreat, fd + "<>");
                    Advance();
                }
                else
                {
                    EmitSimple(TokenType.RedirectIn, fd + "<");
                }
            }
            else
            {
                if (_i + 1 < _input.Length && _input[_i + 1] == '>')
                {
                    EmitSimple(TokenType.Append, fd + ">>");
                    Advance();
                }
                else if (_i + 1 < _input.Length && _input[_i + 1] == '&')
                {
                    EmitSimple(TokenType.GreatAnd, fd + ">&");
                    Advance();
                }
                else if (_i + 1 < _input.Length && _input[_i + 1] == '|')
                {
                    EmitSimple(TokenType.RedirectOut, fd + ">");
                    Advance();
                }
                else
                {
                    EmitSimple(TokenType.RedirectOut, fd + ">");
                }
            }
        }

        private void HandleLParen()
        {
            if (AtWordStart() && _i + 1 < _input.Length && _input[_i + 1] == '(')
            {
                ScanArithCommand();
                return;
            }

            if (_sb.Length > 0 && _parts.Count == 0 && IsValidName(_sb))
            {
                EmitWord();
                EmitSimple(TokenType.LParen, "(");
            }
            else if (AtWordStart())
            {
                EmitSimple(TokenType.LParen, "(");
            }
            else
            {
                AppendLiteral('(');
            }
            Advance();
        }

        private void HandleRParen()
        {
            EmitWord();
            EmitSimple(TokenType.RParen, ")");
            Advance();
        }

        private void HandleLBrace()
        {
            if (AtWordStart())
                EmitSimple(TokenType.LBrace, "{");
            else
                AppendLiteral('{');
            Advance();
        }

        private void HandleRBrace()
        {
            if (AtWordStart())
                EmitSimple(TokenType.RBrace, "}");
            else
                AppendLiteral('}');
            Advance();
        }

        private void ScanArithCommand()
        {
            Advance();
            Advance();
            var depth = 0;
            var sb = new StringBuilder();
            while (_i < _input.Length)
            {
                var c = _input[_i];
                if (c == '(')
                {
                    depth++;
                    sb.Append(c);
                    Advance();
                }
                else if (c == ')')
                {
                    if (depth == 0)
                    {
                        if (_i + 1 < _input.Length && _input[_i + 1] == ')')
                        {
                            Advance();
                            Advance();
                            EmitSimple(TokenType.ArithCommand, sb.ToString());
                            return;
                        }
                        throw new SyntaxError("算术命令 ((...)) 未闭合", _line, _col);
                    }
                    depth--;
                    sb.Append(c);
                    Advance();
                }
                else
                {
                    sb.Append(c);
                    Advance();
                }
            }
            throw new SyntaxError("算术命令 ((...)) 未闭合", _line, _col);
        }

        private void SkipToLineEnd()
        {
            while (_i < _input.Length && _input[_i] != '\n')
                Advance();
        }

        private bool AtWordStart() => _sb.Length == 0 && _parts.Count == 0 && !_hasWord;

        private static bool AllDigits(StringBuilder sb)
        {
            for (var i = 0; i < sb.Length; i++)
            {
                if (!char.IsDigit(sb[i]))
                    return false;
            }
            return true;
        }

        private static bool IsValidName(StringBuilder sb)
        {
            if (sb.Length == 0 || char.IsDigit(sb[0]))
                return false;
            for (var i = 0; i < sb.Length; i++)
            {
                if (!char.IsLetterOrDigit(sb[i]) && sb[i] != '_')
                    return false;
            }
            return true;
        }

        private void AppendLiteral(char c)
        {
            _sb.Append(c);
            _hasWord = true;
        }

        private void AppendLiteralAndAdvance(char c)
        {
            _sb.Append(c);
            _hasWord = true;
            Advance();
        }

        private void FlushLiteral()
        {
            if (_sb.Length > 0)
            {
                _parts.Add(new LiteralPart(_sb.ToString(), _inDoubleQuote));
                _sb.Clear();
            }
        }

        private void EmitWord()
        {
            FlushLiteral();
            if (_parts.Count > 0 || _hasWord)
            {
                var word = new Word(_parts.ToArray());
                _tokens.Add(new Token(TokenType.Word, word.Raw, _line, _col, word));
                _parts.Clear();
                _hasWord = false;
            }
        }

        private void EmitSimple(TokenType type, string text)
        {
            _tokens.Add(new Token(type, text, _line, _col));
        }

        private void Emit(TokenType type, string text)
        {
            _tokens.Add(new Token(type, text, _line, _col));
        }

        private void Advance()
        {
            if (_i < _input.Length && _input[_i] == '\n')
            {
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }
            _i++;
        }
    }
}

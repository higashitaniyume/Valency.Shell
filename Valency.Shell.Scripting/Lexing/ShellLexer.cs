using System.Text;
using Serilog;
using Valency.Shell.Scripting.Ast;

namespace Valency.Shell.Scripting.Lexing;

public sealed class SyntaxError : Exception
{
	public int Line { get; }
	public int Column { get; }

	public SyntaxError(string message, int line, int column)
		: base(string.Format(Resources.SyntaxErrorLocation, message, line, column))
	{
		Line = line;
		Column = column;
	}
}

public static class ShellLexer
{
	public static IReadOnlyList<Token> Tokenize(string input, ILogger? logger = null)
	{
		var tokens = new Lexer(input).Run();
		logger?.ForContext("Src", "lexer")
			.Verbose(Resources.LogLexerTokens, string.Join(' ', tokens.Select(t => t.ToString())));
		return tokens;
	}

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
		private bool _atStatementStart = true;

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
					_atStatementStart = true;
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
					if (!EmitWord())
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
				throw new SyntaxError(Resources.UnclosedDoubleQuote, _line, _col);
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
			throw new SyntaxError(Resources.UnclosedCommandSubstitution, _line, _col);
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
				throw new SyntaxError(Resources.UnclosedBacktick, _line, _col);
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
				throw new SyntaxError(Resources.UnclosedSingleQuote, _line, _col);
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
					_atStatementStart = true;
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
			{
				EmitSimple(TokenType.LBrace, "{");
				_atStatementStart = true;
			}
			else
			{
				AppendLiteral('{');
			}
			Advance();
		}

		private void HandleRBrace()
		{
			if (AtWordStart())
			{
				EmitSimple(TokenType.RBrace, "}");
				_atStatementStart = true;
			}
			else
			{
				AppendLiteral('}');
			}
			Advance();
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

		private bool EmitWord()
		{
			FlushLiteral();
			if (_parts.Count > 0 || _hasWord)
			{
				var word = new Word(_parts.ToArray());
				_parts.Clear();
				_hasWord = false;

				if (IsControlKeyword(word))
				{
					_tokens.Add(new Token(TokenType.Word, word.Raw, _line, _col, word));
					if (ScanKeywordExpression())
						return true;
					_atStatementStart = false;
					return false;
				}

				if (_atStatementStart && word.Raw.StartsWith('$'))
				{
					ScanStatementExpression(word.Raw);
					return true;
				}

				_tokens.Add(new Token(TokenType.Word, word.Raw, _line, _col, word));
				_atStatementStart = false;
			}
			return false;
		}

		private void ScanStatementExpression(string prefix)
		{
			var sb = new StringBuilder(prefix);
			while (_i < _input.Length)
			{
				var c = _input[_i];
				if (c is '\n' or ';' or '{' or '}' or '|' or '&')
					break;

				if (c is '\'' or '"')
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
					continue;
				}

				if (c == '$' && _i + 1 < _input.Length && _input[_i + 1] == '(')
				{
					sb.Append('$');
					sb.Append('(');
					Advance();
					Advance();
					var depth = 1;
					while (_i < _input.Length && depth > 0)
					{
						var ch = _input[_i];
						if (ch == '(')
							depth++;
						else if (ch == ')')
							depth--;
						sb.Append(ch);
						Advance();
					}
					continue;
				}

				sb.Append(c);
				Advance();
			}

			_tokens.Add(new Token(TokenType.Expression, sb.ToString().Trim(), _line, _col));
		}

		private static bool IsControlKeyword(Word word)
		{
			return word.Parts.Count == 1 &&
				   word.Parts[0] is LiteralPart { Quoted: false } lit &&
				   (lit.Text == "if" || lit.Text == "while" || lit.Text == "until" || lit.Text == "for");
		}

		private bool ScanKeywordExpression()
		{
			var save = _i;
			while (_i < _input.Length && char.IsWhiteSpace(_input[_i]) && _input[_i] != '\n')
				Advance();
			if (_i >= _input.Length || _input[_i] != '(')
			{
				_i = save;
				return false;
			}

			var sb = new StringBuilder();
			ScanBalancedExpression(sb);
			_tokens.Add(new Token(TokenType.Expression, sb.ToString(), _line, _col));
			return true;
		}

		private void ScanBalancedExpression(StringBuilder sb)
		{
			Advance();
			var depth = 1;
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
			throw new SyntaxError(Resources.UnclosedExpressionParen, _line, _col);
		}

		private void EmitSimple(TokenType type, string text)
		{
			_tokens.Add(new Token(type, text, _line, _col));
			_atStatementStart = false;
		}

		private void Emit(TokenType type, string text)
		{
			_tokens.Add(new Token(type, text, _line, _col));
			_atStatementStart = false;
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

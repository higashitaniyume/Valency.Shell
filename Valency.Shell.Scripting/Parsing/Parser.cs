using System.Text;
using Serilog;
using Valency.Shell.Scripting.Ast;
using Valency.Shell.Scripting.Lexing;

namespace Valency.Shell.Scripting.Parsing;

public sealed class IncompleteInputException : Exception
{
	public IncompleteInputException() : base(Resources.IncompleteInput) { }
}

public static class Parser
{
	public static Script Parse(string text, ILogger? logger = null)
	{
		return new ParserImpl(ShellLexer.Tokenize(text, logger), logger).ParseScript();
	}

	public static bool IsIncomplete(string text)
	{
		try
		{
			Parse(text);
			return false;
		}
		catch (IncompleteInputException)
		{
			return true;
		}
		catch (SyntaxError)
		{
			return false;
		}
	}

	private sealed class ParserImpl
	{
		private readonly IReadOnlyList<Token> _tokens;
		private readonly ILogger? _logger;
		private int _pos;

		public ParserImpl(IReadOnlyList<Token> tokens, ILogger? logger)
		{
			_tokens = tokens;
			_logger = logger?.ForContext("Src", "parser");
		}

		private Token Current => _tokens[_pos];
		private Token Peek(int offset = 1)
		{
			var index = Math.Min(_pos + offset, _tokens.Count - 1);
			return _tokens[index];
		}

		private Token Advance() => _tokens[_pos++];

		private bool At(TokenType type) => Current.Type == type;

		private bool AtEnd => Current.Type == TokenType.EndOfFile;

		public Script ParseScript()
		{
			var statements = ParseStatements();
			if (!AtEnd)
				throw Error(string.Format(Resources.UnexpectedToken, Current.Text));
			return new Script(statements);
		}

		private IReadOnlyList<Statement> ParseStatements()
		{
			var list = new List<Statement>();
			while (true)
			{
				SkipSeparators();
				if (At(TokenType.RBrace) || AtEnd)
					break;
				var before = _pos;
				var statement = ParseStatement();
				if (_pos == before)
					throw Error(string.Format(Resources.CannotParseToken, Current.Text));
				_logger?.Verbose(Resources.LogParsedStatement, statement.GetType().Name);
				list.Add(statement);
			}
			return list;
		}

		private void SkipSeparators()
		{
			while (At(TokenType.Newline) || At(TokenType.Semi))
				Advance();
		}

		private Statement ParseStatement()
		{
			if (At(TokenType.LBrace))
				return ParseBlock();

			if (At(TokenType.Word))
			{
				var raw = Current.Word!.Raw;
				switch (raw)
				{
					case "if": return ParseIf();
					case "while": return ParseWhile(until: false);
					case "until": return ParseWhile(until: true);
					case "for": return ParseFor();
					case "function": return ParseFunction();
					case "return": return ParseReturn();
					case "break":
						Advance();
						return new BreakStatement();
					case "continue":
						Advance();
						return new ContinueStatement();
				}
			}

			return ParseCommandStatement();
		}

		private BlockStatement ParseBlock()
		{
			Expect(TokenType.LBrace);
			var statements = ParseStatements();
			Expect(TokenType.RBrace);
			return new BlockStatement(statements);
		}

		private Statement ParseIf()
		{
			Advance();
			var condition = ExpectExpression();
			var then = ParseBlock();
			var elseIfs = new List<(string, BlockStatement)>();
			BlockStatement? elseBlock = null;

			while (CurrentWordIs("else"))
			{
				Advance();
				if (CurrentWordIs("if"))
				{
					Advance();
					var cond = ExpectExpression();
					var body = ParseBlock();
					elseIfs.Add((cond, body));
				}
				else
				{
					elseBlock = ParseBlock();
					break;
				}
			}

			return new IfStatement(condition, then, elseIfs, elseBlock);
		}

		private Statement ParseWhile(bool until)
		{
			Advance();
			var condition = ExpectExpression();
			var body = ParseBlock();
			return new WhileStatement(condition, body, until);
		}

		private Statement ParseFor()
		{
			Advance();
			var text = ExpectExpression();
			var (init, cond, post) = SplitForParts(text);
			var body = ParseBlock();
			return new ForStatement(init, cond, post, body);
		}

		private Statement ParseFunction()
		{
			Advance();
			var name = ExpectName();
			Expect(TokenType.LParen);
			var parameters = new List<string>();
			while (!At(TokenType.RParen))
			{
				if (!At(TokenType.Word))
					throw Error(Resources.FunctionParamNeedsVariable);
				var raw = Current.Word!.Raw.TrimEnd(',');
				var paramName = StripDollar(raw);
				if (!IsIdentifier(paramName))
					throw Error(string.Format(Resources.InvalidParameterName, raw));
				parameters.Add(paramName);
				Advance();
			}
			Expect(TokenType.RParen);
			var body = ParseBlock();
			return new FunctionDecl(name, parameters, body);
		}

		private Statement ParseReturn()
		{
			Advance();
			var text = CollectExpressionText();
			return new ReturnStatement(string.IsNullOrWhiteSpace(text) ? null : text);
		}

		private Statement ParseCommandStatement()
		{
			if (At(TokenType.Expression))
				return new ExpressionStatement(Advance().Text);

			if (At(TokenType.Word) && IsVariableWord(Current.Word!))
				return new ExpressionStatement(CollectExpressionText());

			var andOr = ParseAndOr();
			var background = false;
			if (At(TokenType.Background))
			{
				background = true;
				Advance();
			}
			return new CommandStatement(andOr, background);
		}

		private static bool IsVariableWord(Word word) => word.Raw.StartsWith('$');

		private AndOr ParseAndOr()
		{
			var first = ParsePipeline();
			var rest = new List<(Connector Op, Pipeline Pipeline)>();

			while (true)
			{
				if (At(TokenType.AndIf))
				{
					Advance();
					SkipNewlines();
					rest.Add((Connector.And, ParsePipeline()));
				}
				else if (At(TokenType.OrIf))
				{
					Advance();
					SkipNewlines();
					rest.Add((Connector.Or, ParsePipeline()));
				}
				else
				{
					break;
				}
			}

			return new AndOr(first, rest);
		}

		private Pipeline ParsePipeline()
		{
			var negate = false;
			if (At(TokenType.Bang))
			{
				negate = true;
				Advance();
				SkipNewlines();
			}

			var commands = new List<Command> { ParseCommand() };
			while (At(TokenType.Pipe))
			{
				Advance();
				SkipNewlines();
				commands.Add(ParseCommand());
			}
			return new Pipeline(negate, commands);
		}

		private void SkipNewlines()
		{
			while (At(TokenType.Newline))
				Advance();
		}

		private Command ParseCommand()
		{
			var redirects = new List<Redirection>();
			var words = new List<Word>();

			while (true)
			{
				if (At(TokenType.Word))
				{
					words.Add(Advance().Word!);
					continue;
				}
				if (IsRedirectToken(Current.Type))
				{
					redirects.Add(ParseRedirect());
					continue;
				}
				break;
			}

			return new SimpleCommand(redirects, words);
		}

		private Redirection ParseRedirect()
		{
			var token = Advance();
			var kind = token.Type switch
			{
				TokenType.RedirectIn => RedirectionKind.Input,
				TokenType.RedirectOut => RedirectionKind.Output,
				TokenType.Append => RedirectionKind.Append,
				TokenType.LessAnd => RedirectionKind.DupInput,
				TokenType.GreatAnd => RedirectionKind.DupOutput,
				TokenType.LessGreat => RedirectionKind.DupOutputInput,
				TokenType.DLess => RedirectionKind.Heredoc,
				TokenType.DLessDash => RedirectionKind.HeredocDash,
				TokenType.AndGreat => RedirectionKind.AndOutput,
				TokenType.AndGreatAnd => RedirectionKind.AndAppend,
				_ => throw Error(string.Format(Resources.UnknownRedirection, token.Text)),
			};

			if (kind is RedirectionKind.Heredoc or RedirectionKind.HeredocDash)
				throw Error(Resources.HeredocNotSupported);

			var fd = ParseFd(token.Text, kind);
			if (!At(TokenType.Word))
				throw Error(Resources.RedirectionMissingTarget);
			var target = Advance().Word!;
			return new Redirection(fd, kind, target);
		}

		private static int ParseFd(string text, RedirectionKind kind)
		{
			var defaultFd = kind is RedirectionKind.Input or RedirectionKind.DupInput
				or RedirectionKind.DupOutputInput or RedirectionKind.Heredoc or RedirectionKind.HeredocDash
				? 0
				: 1;

			var digits = 0;
			while (digits < text.Length && char.IsDigit(text[digits]))
				digits++;
			if (digits == 0)
				return defaultFd;
			return int.Parse(text[..digits]);
		}

		private static bool IsRedirectToken(TokenType type) => type is
			TokenType.RedirectIn or TokenType.RedirectOut or TokenType.Append or
			TokenType.LessAnd or TokenType.GreatAnd or TokenType.LessGreat or
			TokenType.DLess or TokenType.DLessDash or TokenType.AndGreat or TokenType.AndGreatAnd;

		private string ExpectExpression()
		{
			if (!At(TokenType.Expression))
				throw Error(Resources.ExpectedExpression);
			return Advance().Text;
		}

		private string CollectExpressionText()
		{
			var parts = new List<string>();
			while (true)
			{
				if (At(TokenType.Word))
				{
					parts.Add(Advance().Word!.Raw);
					continue;
				}
				if (At(TokenType.Expression))
				{
					parts.Add(Advance().Text);
					continue;
				}
				break;
			}
			return string.Join(' ', parts);
		}

		private static (string? Init, string? Cond, string? Post) SplitForParts(string text)
		{
			var parts = SplitTopLevel(text, ';');
			string? init = parts.Count > 0 ? TrimOrNull(parts[0]) : null;
			string? cond = parts.Count > 1 ? TrimOrNull(parts[1]) : null;
			string? post = parts.Count > 2 ? TrimOrNull(parts[2]) : null;
			return (init, cond, post);
		}

		private static List<string> SplitTopLevel(string text, char separator)
		{
			var result = new List<string>();
			var sb = new StringBuilder();
			var depth = 0;
			var quote = '\0';
			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];
				if (quote != '\0')
				{
					sb.Append(c);
					if (c == quote)
						quote = '\0';
					continue;
				}
				if (c is '\'' or '"')
				{
					quote = c;
					sb.Append(c);
					continue;
				}
				if (c == '(')
				{
					depth++;
					sb.Append(c);
					continue;
				}
				if (c == ')')
				{
					depth--;
					sb.Append(c);
					continue;
				}
				if (c == separator && depth == 0)
				{
					result.Add(sb.ToString());
					sb.Clear();
					continue;
				}
				sb.Append(c);
			}
			result.Add(sb.ToString());
			return result;
		}

		private static string? TrimOrNull(string text)
		{
			var trimmed = text.Trim();
			return trimmed.Length == 0 ? null : trimmed;
		}

		private static string StripDollar(string raw)
		{
			if (raw.StartsWith('$'))
				raw = raw[1..];
			if (raw.StartsWith('{') && raw.EndsWith('}'))
				raw = raw[1..^1];
			return raw;
		}

		private static bool IsIdentifier(string text)
		{
			if (text.Length == 0 || !char.IsLetter(text[0]) && text[0] != '_')
				return false;
			for (var i = 0; i < text.Length; i++)
			{
				if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
					return false;
			}
			return true;
		}

		private string ExpectName()
		{
			if (!At(TokenType.Word) || !IsIdentifier(Current.Word!.Raw))
				throw Error(Resources.ExpectedIdentifier);
			return Advance().Word!.Raw;
		}

		private bool CurrentWordIs(string word)
		{
			return At(TokenType.Word) &&
				   Current.Word!.Raw.Equals(word, StringComparison.Ordinal);
		}

		private void Expect(TokenType type)
		{
			if (!At(type))
				throw Error(string.Format(Resources.ExpectedToken, type, Current.Text));
			Advance();
		}

		private SyntaxError Error(string message) =>
			new(message, Current.Line, Current.Column);
	}
}

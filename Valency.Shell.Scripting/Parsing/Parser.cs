using Valency.Shell.Scripting.Ast;
using Valency.Shell.Scripting.Lexing;

namespace Valency.Shell.Scripting.Parsing;

public sealed class IncompleteInputException : Exception
{
    public IncompleteInputException() : base("命令未完成") { }
}

public static class Parser
{
    public static Script Parse(string text)
    {
        return new ParserImpl(ShellLexer.Tokenize(text)).ParseScript();
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
        private int _pos;

        public ParserImpl(IReadOnlyList<Token> tokens) => _tokens = tokens;

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
            var body = ParseCompoundList(null);
            if (!AtEnd)
                throw Error($"意外符号 '{Current.Text}'");
            return new Script(body);
        }

        private CompoundList ParseCompoundList(IReadOnlySet<string>? stopWords)
        {
            var entries = new List<Entry>();

            while (true)
            {
                SkipListSeparators();
                if (IsTerminator(stopWords))
                    break;

                var andOr = ParseAndOr();

                var connector = Connector.None;
                if (At(TokenType.Semi))
                {
                    connector = Connector.Semicolon;
                    Advance();
                }
                else if (At(TokenType.Background))
                {
                    connector = Connector.Background;
                    Advance();
                }
                else if (At(TokenType.Newline))
                {
                    connector = Connector.Newline;
                    Advance();
                }

                entries.Add(new Entry(andOr, connector));
            }

            return new CompoundList(entries);
        }

        private void SkipListSeparators()
        {
            while (At(TokenType.Newline) || At(TokenType.Semi))
                Advance();
        }

        private bool IsTerminator(IReadOnlySet<string>? stopWords)
        {
            if (At(TokenType.EndOfFile) || At(TokenType.RParen) || At(TokenType.RBrace))
                return true;
            if (At(TokenType.Word) && stopWords is not null &&
                stopWords.Contains(Current.Word!.Raw.ToLowerInvariant()))
                return true;
            return false;
        }

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
            if (At(TokenType.ArithCommand))
            {
                var expr = Advance().Text;
                return new ArithmeticCommand(expr);
            }

            if (At(TokenType.LBrace))
                return ParseBraceGroup();

            if (At(TokenType.LParen))
                return ParseSubshell();

            if (At(TokenType.Word))
            {
                var raw = Current.Word!.Raw;

                if (raw.Equals("if", StringComparison.OrdinalIgnoreCase))
                    return ParseIf();
                if (raw.Equals("while", StringComparison.OrdinalIgnoreCase))
                    return ParseWhile(until: false);
                if (raw.Equals("until", StringComparison.OrdinalIgnoreCase))
                    return ParseWhile(until: true);
                if (raw.Equals("for", StringComparison.OrdinalIgnoreCase))
                    return ParseFor();
                if (raw.Equals("case", StringComparison.OrdinalIgnoreCase))
                    return ParseCase();
                if (raw.Equals("function", StringComparison.OrdinalIgnoreCase))
                    return ParseFunctionKeyword();

                if (IsIdentifier(raw) && Peek().Type == TokenType.LParen)
                    return ParseFunctionDef();
            }

            return ParseSimpleCommand();
        }

        private Command ParseBraceGroup()
        {
            Expect(TokenType.LBrace);
            var body = ParseCompoundList(new HashSet<string>());
            Expect(TokenType.RBrace);
            return new BraceGroup(body);
        }

        private Command ParseSubshell()
        {
            Expect(TokenType.LParen);
            var body = ParseCompoundList(null);
            Expect(TokenType.RParen);
            return new Subshell(body);
        }

        private Command ParseFunctionKeyword()
        {
            ExpectWord("function");
            var name = ExpectName();
            if (At(TokenType.LParen))
            {
                Advance();
                Expect(TokenType.RParen);
            }
            return new FunctionDef(name, ParseFunctionBody());
        }

        private Command ParseFunctionDef()
        {
            var name = Advance().Word!.Raw;
            Expect(TokenType.LParen);
            Expect(TokenType.RParen);
            return new FunctionDef(name, ParseFunctionBody());
        }

        private Command ParseFunctionBody()
        {
            if (At(TokenType.LBrace))
                return ParseBraceGroup();
            if (At(TokenType.LParen))
                return ParseSubshell();
            if (At(TokenType.Word))
            {
                var raw = Current.Word!.Raw;
                if (raw.Equals("if", StringComparison.OrdinalIgnoreCase))
                    return ParseIf();
                if (raw.Equals("while", StringComparison.OrdinalIgnoreCase))
                    return ParseWhile(until: false);
                if (raw.Equals("until", StringComparison.OrdinalIgnoreCase))
                    return ParseWhile(until: true);
                if (raw.Equals("for", StringComparison.OrdinalIgnoreCase))
                    return ParseFor();
                if (raw.Equals("case", StringComparison.OrdinalIgnoreCase))
                    return ParseCase();
            }
            throw Error("函数体必须是复合命令（{ }、( ) 或 if/while/for/case）");
        }

        private Command ParseIf()
        {
            ExpectWord("if");
            var branches = new List<Branch>();

            var cond = ParseCompoundList(KeywordSet("then"));
            ExpectWord("then");
            var body = ParseCompoundList(KeywordSet("elif", "else", "fi"));
            branches.Add(new Branch(cond, body));

            while (CurrentWordIs("elif"))
            {
                Advance();
                var c = ParseCompoundList(KeywordSet("then"));
                ExpectWord("then");
                var b = ParseCompoundList(KeywordSet("elif", "else", "fi"));
                branches.Add(new Branch(c, b));
            }

            CompoundList? elseBody = null;
            if (CurrentWordIs("else"))
            {
                Advance();
                elseBody = ParseCompoundList(KeywordSet("fi"));
            }

            ExpectWord("fi");
            return new IfCommand(branches, elseBody);
        }

        private Command ParseWhile(bool until)
        {
            Advance();
            var cond = ParseCompoundList(KeywordSet("do"));
            ExpectWord("do");
            var body = ParseCompoundList(KeywordSet("done"));
            ExpectWord("done");
            return new WhileCommand(cond, body, until);
        }

        private Command ParseFor()
        {
            ExpectWord("for");
            var variable = ExpectName();
            IReadOnlyList<Word>? items = null;

            if (CurrentWordIs("in"))
            {
                Advance();
                var list = new List<Word>();
                while (At(TokenType.Word) && !CurrentWordIs("do"))
                {
                    list.Add(Advance().Word!);
                }
                items = list;
            }
            else if (CurrentWordIs("do") || At(TokenType.Semi) || At(TokenType.Newline))
            {
                items = null;
            }
            else
            {
                throw Error("for 循环需要 in 或 do");
            }

            SkipListSeparators();
            ExpectWord("do");
            var body = ParseCompoundList(KeywordSet("done"));
            ExpectWord("done");
            return new ForInCommand(variable, items, body);
        }

        private Command ParseCase()
        {
            ExpectWord("case");
            if (!At(TokenType.Word))
                throw Error("case 需要一个匹配词");
            var word = Advance().Word!;
            ExpectWord("in");

            var arms = new List<CaseArm>();
            SkipNewlines();

            while (!CurrentWordIs("esac"))
            {
                if (AtEnd)
                    throw new IncompleteInputException();

                var patterns = new List<Word>();
                if (!At(TokenType.Word))
                    throw Error("case 分支需要模式");

                while (true)
                {
                    patterns.Add(Advance().Word!);
                    if (At(TokenType.Pipe))
                    {
                        Advance();
                        if (!At(TokenType.Word))
                            throw Error("'|' 后需要模式");
                        continue;
                    }
                    break;
                }

                Expect(TokenType.RParen);
                var body = ParseCaseBody();
                arms.Add(new CaseArm(patterns, body));
                SkipNewlines();
            }

            ExpectWord("esac");
            return new CaseCommand(word, arms);
        }

        private CompoundList ParseCaseBody()
        {
            var entries = new List<Entry>();
            while (true)
            {
                if (At(TokenType.EndOfFile) || At(TokenType.RParen) || At(TokenType.RBrace))
                    break;
                if (CurrentWordIs("esac"))
                    break;
                if (At(TokenType.Semi) && Peek().Type == TokenType.Semi)
                {
                    Advance();
                    Advance();
                    break;
                }

                if (At(TokenType.Newline) || At(TokenType.Semi))
                {
                    Advance();
                    continue;
                }

                var andOr = ParseAndOr();
                var connector = Connector.None;
                if (At(TokenType.Semi) && Peek().Type == TokenType.Semi)
                {
                    Advance();
                    Advance();
                    entries.Add(new Entry(andOr, Connector.Semicolon));
                    break;
                }
                if (At(TokenType.Semi))
                {
                    connector = Connector.Semicolon;
                    Advance();
                }
                else if (At(TokenType.Background))
                {
                    connector = Connector.Background;
                    Advance();
                }
                else if (At(TokenType.Newline))
                {
                    connector = Connector.Newline;
                    Advance();
                }

                entries.Add(new Entry(andOr, connector));
            }

            return new CompoundList(entries);
        }

        private Command ParseSimpleCommand()
        {
            var assignments = new List<Assignment>();
            var redirections = new List<Redirection>();
            var words = new List<Word>();

            while (true)
            {
                if (At(TokenType.Word))
                {
                    var word = Current.Word!;
                    if (words.Count == 0 && TrySplitAssignment(word, out var name, out var append, out var value))
                    {
                        assignments.Add(new Assignment(name, append, value));
                        Advance();
                        continue;
                    }
                    words.Add(word);
                    Advance();
                    continue;
                }

                if (IsRedirectToken(Current.Type))
                {
                    redirections.Add(ParseRedirect());
                    continue;
                }

                break;
            }

            return new SimpleCommand(assignments, redirections, words);
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
                _ => throw Error($"未知重定向 '{token.Text}'"),
            };

            if (kind is RedirectionKind.Heredoc or RedirectionKind.HeredocDash)
                throw Error("here-doc 暂不支持");

            var fd = ParseFd(token.Text, kind);
            if (!At(TokenType.Word))
                throw Error("重定向缺少目标");
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

        private static bool TrySplitAssignment(Word word, out string name, out bool append, out Word value)
        {
            name = string.Empty;
            append = false;
            value = Word.Empty;

            if (word.Parts.Count == 0 || word.Parts[0] is not LiteralPart { Quoted: false } first)
                return false;

            var text = first.Text;
            var eq = text.IndexOf('=');
            if (eq <= 0)
                return false;

            var nameCandidate = text[..eq];
            if (nameCandidate.EndsWith('+'))
            {
                nameCandidate = nameCandidate[..^1];
                append = true;
            }

            if (!IsIdentifier(nameCandidate))
                return false;

            name = nameCandidate;
            var valueParts = new List<WordPart>();
            var rest = text[(eq + 1)..];
            if (rest.Length > 0)
                valueParts.Add(new LiteralPart(rest, false));
            for (var i = 1; i < word.Parts.Count; i++)
                valueParts.Add(word.Parts[i]);
            value = new Word(valueParts);
            return true;
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

        private static HashSet<string> KeywordSet(params string[] words)
        {
            var set = new HashSet<string>();
            foreach (var word in words)
                set.Add(word.ToLowerInvariant());
            return set;
        }

        private string ExpectName()
        {
            if (!At(TokenType.Word) || !IsIdentifier(Current.Word!.Raw))
                throw Error("需要标识符");
            return Advance().Word!.Raw;
        }

        private bool CurrentWordIs(string word)
        {
            return At(TokenType.Word) &&
                   Current.Word!.Raw.Equals(word, StringComparison.OrdinalIgnoreCase);
        }

        private void ExpectWord(string word)
        {
            if (!CurrentWordIs(word))
                throw Error($"需要 '{word}'");
            Advance();
        }

        private void Expect(TokenType type)
        {
            if (!At(type))
                throw Error($"需要 '{type}'，实际为 '{Current.Text}'");
            Advance();
        }

        private SyntaxError Error(string message) =>
            new(message, Current.Line, Current.Column);
    }
}

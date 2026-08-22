using Valency.Shell.Core.Expansion;

namespace Valency.Shell.Prompting;

public sealed class PromptFormatter
{
	public const string PlainTemplate = "$USER$CONN$PWD$SHARP ";
	public const string KaliTemplate = "┌──($USER$CONN$HOST)-[$PWD]\n└─$SHARP ";

	private const string Reset = "\x1b[0m";
	private const string Green = "\x1b[32m";
	private const string BoldRed = "\x1b[1;31m";
	private const string BoldBlue = "\x1b[1;34m";

	private readonly Func<bool> _isAdmin;
	private readonly VariableExpander _expander;

	public PromptFormatter(Func<bool>? isAdmin = null)
	{
		_isAdmin = isAdmin ?? IsRootOrAdmin;
		_expander = new VariableExpander(new PromptVariableSource(_isAdmin));
	}

	public static bool IsRootOrAdmin()
	{
		if (OperatingSystem.IsWindows())
		{
			try
			{
				using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
				return new System.Security.Principal.WindowsPrincipal(identity)
					.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
			}
			catch (Exception)
			{
				return false;
			}
		}

		return Environment.UserName == "root";
	}

	public Prompt BuildPlain()
	{
		var admin = _isAdmin();
		var user = Environment.UserName;
		var dir = PromptVariableSource.AbbreviateHome(Environment.CurrentDirectory);
		var sharp = admin ? "#" : "$";
		var userColor = admin ? BoldRed : Green;

		var line = userColor + user + Reset
			+ Green + "@" + Reset
			+ BoldBlue + dir + Reset
			+ BoldRed + sharp + Reset
			+ " ";

		return new Prompt(line, line, VisibleLength(line));
	}

	public Prompt BuildKali()
	{
		var admin = _isAdmin();
		var user = Environment.UserName;
		var host = Environment.MachineName;
		var dir = PromptVariableSource.AbbreviateHome(Environment.CurrentDirectory);
		var sharp = admin ? "#" : "$";
		var userColor = admin ? BoldRed : Green;

		var line1 = Green + "┌──(" + Reset
			+ userColor + user + Reset
			+ Green + "@" + Reset
			+ Green + host + Reset
			+ Green + ")-[" + Reset
			+ BoldBlue + dir + Reset
			+ Green + "]" + Reset;

		var line2 = Green + "└─" + Reset
			+ BoldRed + sharp + Reset
			+ " ";

		return new Prompt(line1 + "\n" + line2, line2, VisibleLength(line2));
	}

	public Prompt BuildCustom(string template)
	{
		var expanded = _expander.ExpandText(template);
		return FromText(expanded);
	}

	public static Prompt FromText(string expanded)
	{
		var lastNewline = expanded.LastIndexOf('\n');
		var lastLine = lastNewline >= 0 ? expanded[(lastNewline + 1)..] : expanded;
		return new Prompt(expanded, lastLine, VisibleLength(lastLine));
	}

	public static int VisibleLength(string text)
	{
		var length = 0;
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
			{
				var j = i + 2;
				while (j < text.Length && text[j] != 'm' && !(text[j] >= 'A' && text[j] <= 'Z'))
					j++;
				if (j < text.Length && (text[j] == 'm' || (text[j] >= 'A' && text[j] <= 'Z')))
				{
					i = j;
					continue;
				}
			}
			length++;
		}
		return length;
	}

	public static string StripAnsi(string text)
	{
		if (text.IndexOf('\x1b') < 0)
			return text;

		var chars = new List<char>(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
			{
				var j = i + 2;
				while (j < text.Length && text[j] != 'm' && !(text[j] >= 'A' && text[j] <= 'Z'))
					j++;
				if (j < text.Length && (text[j] == 'm' || (text[j] >= 'A' && text[j] <= 'Z')))
				{
					i = j;
					continue;
				}
			}
			chars.Add(text[i]);
		}
		return new string(chars.ToArray());
	}
}

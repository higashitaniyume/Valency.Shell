using Valency.Shell.Core.Builtins;
using Valency.Shell.Prompting;

namespace Valency.Shell.Builtins;

public sealed class PromptCommand : IBuiltinCommand
{
	private readonly PromptSettings _settings;

	public PromptCommand(PromptSettings settings)
	{
		_settings = settings;
	}

	public CommandSpec Spec { get; } = new()
	{
		Name = BuiltinNames.Prompt,
		Summary = Resources.PromptSummary,
		Positionals =
		[
			Resources.PromptPositionalStyle,
			Resources.PromptPositionalTemplate,
		],
	};

	public int Execute(ParseResult args, IShellContext context)
	{
		if (args.Positionals.Count == 0)
		{
			Console.Out.WriteLine(string.Format(Resources.PromptCurrentStyle, _settings.Style));
			if (_settings.Style == PromptSettings.Custom)
				Console.Out.WriteLine(string.Format(Resources.PromptCustomTemplate, _settings.CustomTemplate));
			return 0;
		}

		switch (args.Positionals[0].ToLowerInvariant())
		{
			case PromptSettings.Plain:
				_settings.Style = PromptSettings.Plain;
				return 0;
			case PromptSettings.Kali:
				_settings.Style = PromptSettings.Kali;
				return 0;
			case PromptSettings.Custom:
				_settings.Style = PromptSettings.Custom;
				if (args.Positionals.Count > 1)
					_settings.CustomTemplate = string.Join(" ", args.Positionals.Skip(1));
				return 0;
			default:
				Console.Error.WriteLine(string.Format(Resources.PromptUnknownStyle, args.Positionals[0]));
				return 2;
		}
	}
}

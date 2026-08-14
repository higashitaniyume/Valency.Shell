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
        Summary = "查看或切换提示符风格。",
        Positionals =
        [
            "[style] plain | kali | custom",
            "[template...] 自定义模板（style 为 custom 时）",
        ],
    };

    public int Execute(ParseResult args, IShellContext context)
    {
        if (args.Positionals.Count == 0)
        {
            Console.Out.WriteLine($"当前提示符风格: {_settings.Style}");
            if (_settings.Style == PromptSettings.Custom)
                Console.Out.WriteLine($"自定义模板: {_settings.CustomTemplate}");
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
                Console.Error.WriteLine($"prompt: 未知风格 '{args.Positionals[0]}'，可用: plain | kali | custom [模板]");
                return 2;
        }
    }
}

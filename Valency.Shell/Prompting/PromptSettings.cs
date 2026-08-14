namespace Valency.Shell.Prompting;

public sealed class PromptSettings
{
    public const string Plain = "plain";
    public const string Kali = "kali";
    public const string Custom = "custom";

    public string Style { get; set; } = Kali;
    public string CustomTemplate { get; set; } = PromptFormatter.PlainTemplate;

    public Prompt Build(PromptFormatter formatter)
    {
        return Style switch
        {
            Plain => formatter.BuildPlain(),
            Custom => formatter.BuildCustom(CustomTemplate),
            _ => formatter.BuildKali(),
        };
    }
}

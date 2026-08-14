namespace Valency.Shell.Prompting;

public readonly record struct Prompt(string Raw, string LastLine, int CursorOffset);

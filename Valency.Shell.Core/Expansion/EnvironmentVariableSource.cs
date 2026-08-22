namespace Valency.Shell.Core.Expansion;

public sealed class EnvironmentVariableSource : IVariableSource
{
	public bool TryGet(string name, out string? value)
	{
		value = Environment.GetEnvironmentVariable(name);
		return value is not null;
	}
}

namespace Valency.Shell.Core.Expansion;

public interface IVariableSource
{
	bool TryGet(string name, out string? value);
}

using Valency.Shell.Core.Expansion;
using Valency.Shell.Scripting.Ast;

namespace Valency.Shell.Scripting.Eval;

public sealed class ShellState
{
	private readonly Dictionary<string, string> _vars = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _exported = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, FunctionDecl> Functions { get; } = new(StringComparer.OrdinalIgnoreCase);

	public int LastExitCode { get; set; }
	public string ScriptName { get; set; } = "valency";
	public IReadOnlyList<string> PositionalArgs { get; set; } = [];
	public string CurrentDirectory { get; set; } = Environment.CurrentDirectory;
	public long LastBackgroundPid { get; set; }
	public bool ExitRequested { get; set; }
	public int ExitCode { get; set; }

	public ShellState()
	{
		SeedEnvironment();
	}

	private void SeedEnvironment()
	{
		foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
		{
			var key = entry.Key?.ToString();
			if (key is not null)
				_vars[key] = entry.Value?.ToString() ?? string.Empty;
		}
	}

	public bool TryGetVariable(string name, out string value) => _vars.TryGetValue(name, out value!);

	public string? GetVariable(string name) => _vars.TryGetValue(name, out var value) ? value : null;

	public void SetVariable(string name, string value, bool exported = false)
	{
		_vars[name] = value;
		if (exported)
		{
			_exported.Add(name);
			Environment.SetEnvironmentVariable(name, value);
		}
	}

	public void ExportVariable(string name)
	{
		if (_vars.TryGetValue(name, out var value))
		{
			_exported.Add(name);
			Environment.SetEnvironmentVariable(name, value);
		}
	}

	public void UnsetVariable(string name)
	{
		_vars.Remove(name);
		if (_exported.Remove(name))
			Environment.SetEnvironmentVariable(name, null);
	}
}

public sealed class StateVariableSource(ShellState state) : IVariableSource
{
	public bool TryGet(string name, out string? value)
	{
		switch (name)
		{
			case "?":
				value = state.LastExitCode.ToString();
				return true;
			case "#":
				value = state.PositionalArgs.Count.ToString();
				return true;
			case "@":
			case "*":
				value = string.Join(" ", state.PositionalArgs);
				return true;
			case "$":
				value = Environment.ProcessId.ToString();
				return true;
			case "!":
				value = state.LastBackgroundPid.ToString();
				return true;
		}

		if (name.Length == 1 && char.IsDigit(name[0]))
		{
			var index = name[0] - '0';
			value = index == 0
				? state.ScriptName
				: index - 1 < state.PositionalArgs.Count
					? state.PositionalArgs[index - 1]
					: string.Empty;
			return true;
		}

		if (state.TryGetVariable(name, out var v))
		{
			value = v;
			return true;
		}

		value = null;
		return false;
	}
}

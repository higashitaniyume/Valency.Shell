namespace Valency.Shell.Scripting.Eval;

public enum ControlFlowKind
{
	Exit,
	Return,
	Break,
	Continue,
}

public sealed class ControlFlowException : Exception
{
	public ControlFlowKind Kind { get; }
	public int Code { get; }

	public ControlFlowException(ControlFlowKind kind, int code)
		: base(kind.ToString())
	{
		Kind = kind;
		Code = code;
	}
}

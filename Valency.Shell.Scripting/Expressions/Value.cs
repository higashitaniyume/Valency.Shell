using System.Globalization;

namespace Valency.Shell.Scripting.Expressions;

public enum ValueKind
{
    Int,
    Str,
    Bool,
}

public readonly struct Value
{
    private readonly long _int;
    private readonly string _str;
    private readonly bool _bool;

    public ValueKind Kind { get; }

    private Value(long i, string s, bool b, ValueKind kind)
    {
        _int = i;
        _str = s;
        _bool = b;
        Kind = kind;
    }

    public static Value Int(long v) => new(v, string.Empty, false, ValueKind.Int);
    public static Value Str(string v) => new(0, v, false, ValueKind.Str);
    public static Value Bool(bool v) => new(0, string.Empty, v, ValueKind.Bool);

    public static Value FromString(string v)
    {
        if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return Int(n);
        return Str(v);
    }

    public long AsInt() => Kind switch
    {
        ValueKind.Int => _int,
        ValueKind.Bool => _bool ? 1 : 0,
        _ => long.TryParse(_str, out var n) ? n : 0,
    };

    public string AsString() => Kind switch
    {
        ValueKind.Str => _str,
        ValueKind.Bool => _bool ? "true" : "false",
        _ => _int.ToString(CultureInfo.InvariantCulture),
    };

    public bool Truthy => Kind switch
    {
        ValueKind.Bool => _bool,
        ValueKind.Int => _int != 0,
        _ => _str.Length > 0,
    };

    public override string ToString() => AsString();
}

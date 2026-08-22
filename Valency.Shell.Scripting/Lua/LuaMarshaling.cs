using MoonSharp.Interpreter;

namespace Valency.Shell.Scripting.Lua;

/// <summary>
///     Converts Lua call arguments into argv strings and redirect specifications.
///     A trailing table whose keys are all recognized option names is treated as options:
///     out / err / append / merge / input.
/// </summary>
internal static class LuaMarshaling
{
    private static readonly string[] OptionKeys = ["out", "err", "append", "merge", "input"];

    public static bool IsOptionsTable(DynValue value)
    {
        if (value.Type != DataType.Table)
            return false;

        var arrayLength = value.Table.Length;
        if (arrayLength > 0)
            return false;

        foreach (var pair in value.Table.Pairs)
        {
            if (pair.Key.Type != DataType.String || !OptionKeys.Contains(pair.Key.String))
                return false;
        }
        return true;
    }

    /// <summary>
    ///     Marshals call arguments (starting at <paramref name="skip" />) into argv strings.
    ///     Array tables flatten into multiple arguments; a trailing options table yields redirects.
    /// </summary>
    public static (List<string> Argv, DynValue? Options) ToArgv(CallbackArguments args, int skip)
    {
        var argv = new List<string>();
        DynValue? options = null;

        for (var i = skip; i < args.Count; i++)
        {
            var value = args[i];
            if (options is null && IsOptionsTable(value))
            {
                options = value;
                continue;
            }
            AppendValue(argv, value, i);
        }

        return (argv, options);
    }

    internal static void AppendValue(List<string> argv, DynValue value, int position)
    {
        switch (value.Type)
        {
            case DataType.String:
                argv.Add(value.String);
                return;
            case DataType.Number:
                argv.Add(value.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            case DataType.Boolean:
                argv.Add(value.Boolean ? "true" : "false");
                return;
            case DataType.Table:
                for (var i = 1; i <= value.Table.Length; i++)
                    AppendValue(argv, value.Table.Get(i), position);
                return;
            default:
                throw Errors.BadArgument(position, value);
        }
    }

    public static IReadOnlyList<LuaRedirect>? ToRedirects(DynValue? options)
    {
        if (options is not { } opts)
            return null;

        var redirects = new List<LuaRedirect>();
        var append = GetFlag(opts, "append");
        var merge = GetFlag(opts, "merge");
        var hasOutput = GetString(opts, "out") is not null;

        if (GetString(opts, "out") is { } output)
            redirects.Add(new LuaRedirect(1, append ? LuaRedirectMode.Append : LuaRedirectMode.Output, output));
        if (GetString(opts, "err") is { } error)
            redirects.Add(new LuaRedirect(2, append ? LuaRedirectMode.Append : LuaRedirectMode.Output, error));
        if (GetString(opts, "input") is { } input)
            redirects.Add(new LuaRedirect(0, LuaRedirectMode.Input, input));
        if (merge)
        {
            if (!hasOutput)
                throw Errors.MergeNeedsOut();
            redirects.Add(new LuaRedirect(2, LuaRedirectMode.DupOutput, "1"));
        }

        return redirects.Count > 0 ? redirects : null;
    }

    private static bool GetFlag(DynValue opts, string key)
    {
        var value = opts.Table.Get(key);
        return value.Type == DataType.Boolean && value.Boolean;
    }

    private static string? GetString(DynValue opts, string key)
    {
        var value = opts.Table.Get(key);
        if (value.IsNil())
            return null;
        if (value.Type != DataType.String)
            throw Errors.OptionValueString(key);
        return value.String;
    }
}

internal static class Errors
{
    public static ScriptRuntimeException BadArgument(int position, DynValue value)
        => new(string.Format(Resources.LuaArgNotString, position + 1, value.Type.ToString().ToLowerInvariant()));

    public static ScriptRuntimeException MissingCommand()
        => new(Resources.LuaMissingCommand);

    public static ScriptRuntimeException ArgString(string name)
        => new(string.Format(Resources.LuaArgString, name));

    public static ScriptRuntimeException UnknownOptionKey(string key)
        => new(string.Format(Resources.LuaUnknownOptionKey, key));

    public static ScriptRuntimeException OptionValueString(string key)
        => new(string.Format(Resources.LuaOptionValueString, key));

    public static ScriptRuntimeException MergeNeedsOut()
        => new(Resources.LuaMergeNeedsOut);

    public static ScriptRuntimeException PipeStageEmpty(int index)
        => new(string.Format(Resources.LuaPipeStageEmpty, index));

    public static ScriptRuntimeException PipeStageForm(int index)
        => new(string.Format(Resources.LuaPipeStageForm, index));
}

using MoonSharp.Interpreter;
using Script = MoonSharp.Interpreter.Script;

namespace Valency.Shell.Tests.Scripting;

public class LuaSpikeTests
{
    [Fact]
    public void DoString_EvaluatesExpressions()
    {
        var script = new Script();
        Assert.Equal(3, script.DoString("return 1 + 2").Number);
    }

    [Fact]
    public void FullSemantics_ClosureMetatableVarargsMultipleReturns()
    {
        var script = new Script();
        var result = script.DoString("""
            local add = function(a, b) return a + b end
            local counter = (function() local n = 0 return function() n = n + 1 return n end end)()
            counter() counter()
            local t = setmetatable({}, { __index = function(_, k) return k .. '!' end })
            local function varargs(...) return select('#', ...), ... end
            local n, a, b, c = varargs(10, 'x', true)
            return add(1, 2), counter(), t.foo, n, a, b, c
            """);
        Assert.Equal(3, result.Tuple[0].Number);
        Assert.Equal(3, result.Tuple[1].Number);
        Assert.Equal("foo!", result.Tuple[2].String);
        Assert.Equal(3, result.Tuple[3].Number);
        Assert.Equal(10, result.Tuple[4].Number);
        Assert.Equal("x", result.Tuple[5].String);
        Assert.True(result.Tuple[6].Boolean);
    }

    [Fact]
    public void Globals_MetaTable_IndexProxy_IsHonored()
    {
        var script = new Script();
        var meta = DynValue.NewTable(script);
        meta.Table.Set("__index", DynValue.NewCallback(new CallbackFunction((ctx, args) =>
        {
            var key = args[args.Count - 1].String;
            return DynValue.NewNumber(key.Length);
        }, "__index")));
        script.Globals.MetaTable = meta.Table;

        var result = script.DoString("return undefinedGlobalName");
        Assert.Equal(19, result.Number);
    }

    [Fact]
    public void ClrDelegate_Registration_And_Marshaling()
    {
        var script = new Script();
        script.Globals["twice"] = (Func<double, double>)(x => x * 2);
        script.Globals["pick"] = (Func<DynValue, DynValue, DynValue>)((a, b) => a.Type == DataType.Number ? a : b);
        script.Globals["tableOf"] = (Func<DynValue>)(() =>
        {
            var t = DynValue.NewTable(script);
            t.Table.Set("ok", DynValue.NewString("yes"));
            return t;
        });

        Assert.Equal(10, script.DoString("return twice(5)").Number);
        Assert.Equal(7, script.DoString("return pick('x', 7)").Number);
        Assert.Equal("yes", script.DoString("return tableOf().ok").String);
    }

    [Fact]
    public void Errors_CarryTypeAndLineInfo()
    {
        var script = new Script();
        var syntax = Assert.Throws<SyntaxErrorException>(() => script.DoString("if then"));
        Assert.NotEmpty(syntax.DecoratedMessage);
        Assert.Throws<ScriptRuntimeException>(() => script.DoString("error('boom')"));
    }

    [Fact]
    public void StandardLibrary_DefaultPreset_Available()
    {
        var script = new Script();
        Assert.Equal("a,b", script.DoString("return table.concat({'a','b'}, ',')").String);
        Assert.Equal("5.00", script.DoString("return string.format('%.2f', 5)").String);
        Assert.True(script.DoString("return os.getenv('PATH') ~= nil").Boolean);
    }
}

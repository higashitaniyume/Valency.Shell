using System.Runtime.CompilerServices;
using MoonSharp.Interpreter;

namespace Valency.Shell.Scripting.Lua;

/// <summary>
///     Fluent chains over array tables in the spirit of PowerShell pipelines:
///     ls():filter(f):map(g):sort():echo(). Attaching only a metatable keeps the
///     value a plain Lua table - ipairs/pairs/# behave exactly as before.
/// </summary>
internal static class LuaQuery
{
	private static readonly ConditionalWeakTable<Script, Table> Metas = [];

	public static DynValue Wrap(Script script, DynValue table)
	{
		if (table.Type == DataType.Table)
			table.Table.MetaTable = GetMeta(script);
		return table;
	}

	private static Table GetMeta(Script script)
	{
		if (Metas.TryGetValue(script, out var meta))
			return meta;

		// 方法表放在 __index 下——Lua 只对 metatable 的 __index 字段做缺键查找
		var methodsValue = DynValue.NewTable(script);
		methodsValue.Table.Set("filter", DynValue.NewCallback(new CallbackFunction(Filter, "filter")));
		methodsValue.Table.Set("map", DynValue.NewCallback(new CallbackFunction(Map, "map")));
		methodsValue.Table.Set("sort", DynValue.NewCallback(new CallbackFunction(Sort, "sort")));
		methodsValue.Table.Set("reverse", DynValue.NewCallback(new CallbackFunction(Reverse, "reverse")));
		methodsValue.Table.Set("take", DynValue.NewCallback(new CallbackFunction(Take, "take")));
		methodsValue.Table.Set("echo", DynValue.NewCallback(new CallbackFunction(Echo, "echo")));

		meta = DynValue.NewTable(script).Table;
		meta.Set("__index", methodsValue);
		Metas.Add(script, meta);
		return meta;
	}

	private static Table Self(CallbackArguments args)
	{
		if (args.Count < 1 || args[0].Type != DataType.Table)
			throw Errors.ChainSelf();
		return args[0].Table;
	}

	private static DynValue Filter(ScriptExecutionContext ctx, CallbackArguments args)
	{
		var self = Self(args);
		var predicate = ArgFunction(args, "predicate");
		var result = DynValue.NewTable(ctx.OwnerScript);
		var n = 0;
		for (var i = 1; i <= self.Length; i++)
		{
			var item = self.Get(i);
			if (ctx.OwnerScript.Call(predicate, item).CastToBool())
				result.Table.Set(DynValue.NewNumber(++n), item);
		}
		return Wrap(ctx.OwnerScript, result);
	}

	private static DynValue Map(ScriptExecutionContext ctx, CallbackArguments args)
	{
		var self = Self(args);
		var fn = ArgFunction(args, "function");
		var result = DynValue.NewTable(ctx.OwnerScript);
		var n = 0;
		for (var i = 1; i <= self.Length; i++)
		{
			var mapped = ctx.OwnerScript.Call(fn, self.Get(i));
			if (mapped.IsNil())
				continue; // map 丢弃 nil 结果，保持数组稠密
			result.Table.Set(DynValue.NewNumber(++n), mapped);
		}
		return Wrap(ctx.OwnerScript, result);
	}

	private static DynValue Sort(ScriptExecutionContext ctx, CallbackArguments args)
	{
		var self = Self(args);
		var items = Items(self);

		if (args.Count >= 2 && args[1].Type == DataType.Function)
		{
			var cmp = args[1];
			items.Sort((a, b) => ctx.OwnerScript.Call(cmp, a, b).CastToBool() ? -1 : 1);
		}
		else
		{
			items.Sort(CompareDefault);
		}

		return Wrap(ctx.OwnerScript, FromItems(ctx.OwnerScript, items));
	}

	private static DynValue Reverse(ScriptExecutionContext ctx, CallbackArguments args)
	{
		var items = Items(Self(args));
		items.Reverse();
		return Wrap(ctx.OwnerScript, FromItems(ctx.OwnerScript, items));
	}

	private static DynValue Take(ScriptExecutionContext ctx, CallbackArguments args)
	{
		var self = Self(args);
		if (args.Count < 2 || args[1].Type != DataType.Number)
			throw Errors.ArgString("n");
		var count = Math.Max(0, (int)args[1].Number);
		var items = Items(self).Take(count).ToList();
		return Wrap(ctx.OwnerScript, FromItems(ctx.OwnerScript, items));
	}

	private static DynValue Echo(ScriptExecutionContext ctx, CallbackArguments args)
	{
		var text = LuaRenderer.Render(args[0]);
		if (text.Length > 0)
			Console.Out.WriteLine(text);
		return DynValue.Nil; // 类似 PS 的 Out-Host：终结输出，不产生值（也避免 REPL 二次回显）
	}

	// ---- 辅助 ----

	private static DynValue ArgFunction(CallbackArguments args, string name)
	{
		if (args.Count < 2 || args[1].Type != DataType.Function)
			throw Errors.ArgFunction(name);
		return args[1];
	}

	private static int CompareDefault(DynValue a, DynValue b)
	{
		if (a.Type == DataType.Number && b.Type == DataType.Number)
			return a.Number.CompareTo(b.Number);
		return string.CompareOrdinal(LuaRenderer.CellText(a), LuaRenderer.CellText(b));
	}

	private static List<DynValue> Items(Table table)
	{
		var items = new List<DynValue>(table.Length);
		for (var i = 1; i <= table.Length; i++)
			items.Add(table.Get(i));
		return items;
	}

	private static DynValue FromItems(Script script, List<DynValue> items)
	{
		var result = DynValue.NewTable(script);
		for (var i = 0; i < items.Count; i++)
			result.Table.Set(DynValue.NewNumber(i + 1), items[i]);
		return result;
	}
}

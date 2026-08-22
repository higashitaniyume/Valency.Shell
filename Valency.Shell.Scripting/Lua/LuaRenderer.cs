using System.Text;
using MoonSharp.Interpreter;

namespace Valency.Shell.Scripting.Lua;

/// <summary>
///     Terminal rendering for Lua values, in the spirit of PowerShell's Format-Table /
///     Format-List: arrays of uniform tables become aligned tables, a single map becomes
///     a key/value listing, scalars print as-is.
/// </summary>
internal static class LuaRenderer
{
	private const int MaxCellWidth = 48;
	private const int MaxRows = 100;

	public static string Render(DynValue value)
	{
		return value.Type switch
		{
			DataType.Nil or DataType.Void => string.Empty,
			DataType.Tuple => string.Join("\t",
				value.Tuple.Where(v => !v.IsNil()).Select(Render).Where(s => s.Length > 0)),
			DataType.Table => RenderTable(value.Table),
			_ => value.ToPrintString(),
		};
	}

	private static string RenderTable(Table table)
	{
		// env 代理：本体恒空，按标记渲染进程环境快照
		if (table.MetaTable is { } meta && meta.Get("__valency_env").CastToBool())
			return FormatKeyValueList(Environment.GetEnvironmentVariables()
				.Cast<System.Collections.DictionaryEntry>()
				.Select(e => (Key: e.Key?.ToString(), Value: e.Value?.ToString() ?? string.Empty))
				.Where(p => p.Key is not null)
				.Select(p => (p.Key!, p.Value))
				.OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase)
				.ToList());

		var length = table.Length;
		if (length == 0)
			return RenderMap(table);

		var items = new List<DynValue>(length);
		for (var i = 1; i <= length; i++)
			items.Add(table.Get(i));

		if (items.All(v => v.Type == DataType.Table))
			return RenderGrid(items.Select(v => v.Table).ToList());

		var sb = new StringBuilder();
		var shown = 0;
		foreach (var item in items)
		{
			if (shown++ == MaxRows)
			{
				sb.AppendLine(string.Format(Resources.LuaMoreRows, length - MaxRows));
				break;
			}
			sb.AppendLine(CellText(item));
		}
		return sb.ToString().TrimEnd('\r', '\n');
	}

	/// <summary>key : value 列表（对应 Format-List）。</summary>
	private static string RenderMap(Table table)
	{
		var pairs = table.Pairs
			.Where(p => p.Key.Type == DataType.String)
			.Select(p => (p.Key.String, CellText(p.Value)))
			.ToList();
		return FormatKeyValueList(pairs);
	}

	private static string FormatKeyValueList(List<(string Key, string Value)> pairs)
	{
		if (pairs.Count == 0)
			return "{}";

		var keyWidth = pairs.Max(p => p.Key.Length);
		var sb = new StringBuilder();
		foreach (var (key, text) in pairs)
			sb.AppendLine(key.PadRight(keyWidth) + " : " + text);
		return sb.ToString().TrimEnd('\r', '\n');
	}

	/// <summary>同构 table 数组 → 对齐表格（对应 Format-Table）。</summary>
	private static string RenderGrid(List<Table> rows)
	{
		if (rows.Count == 0)
			return "{}";

		var columns = new List<string>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var row in rows)
		{
			foreach (var pair in row.Pairs)
			{
				if (pair.Key.Type == DataType.String && seen.Add(pair.Key.String))
					columns.Add(pair.Key.String);
			}
		}
		if (columns.Count == 0)
			return "{}";

		var limited = rows.Take(MaxRows + 1).ToList();
		var cells = new List<string?[]>();
		var numeric = new bool[columns.Count];

		foreach (var row in limited)
		{
			var cellRow = new string?[columns.Count];
			for (var c = 0; c < columns.Count; c++)
			{
				var value = row.Get(columns[c]);
				if (value.IsNil())
					continue;
				numeric[c] |= value.Type == DataType.Number;
				cellRow[c] = Truncate(CellText(value));
			}
			cells.Add(cellRow);
		}

		var widths = new int[columns.Count];
		for (var c = 0; c < columns.Count; c++)
		{
			widths[c] = columns[c].Length;
			foreach (var cellRow in cells)
				widths[c] = Math.Max(widths[c], cellRow[c]?.Length ?? 0);
		}

		var sb = new StringBuilder();
		sb.AppendLine(FormatRow(columns.Select((k, c) => k.PadRight(widths[c])).ToList()));
		foreach (var cellRow in cells)
		{
			var fields = new string[columns.Count];
			for (var c = 0; c < columns.Count; c++)
			{
				var text = cellRow[c] ?? string.Empty;
				fields[c] = numeric[c] ? text.PadLeft(widths[c]) : text.PadRight(widths[c]);
			}
			sb.AppendLine(FormatRow(fields.ToList()));
		}

		if (rows.Count > MaxRows)
			sb.AppendLine(string.Format(Resources.LuaMoreRows, rows.Count - MaxRows));
		return sb.ToString().TrimEnd('\r', '\n');
	}

	private static string FormatRow(List<string> fields) => string.Join("  ", fields);

	private static string Truncate(string text) =>
		text.Length <= MaxCellWidth ? text : text[..(MaxCellWidth - 1)] + "…";

	internal static string CellText(DynValue value) => value.Type switch
	{
		DataType.Nil => string.Empty,
		DataType.String => value.String,
		DataType.Table => value.Table.Length > 0
			? string.Join(", ", ScalarsOf(value.Table))
			: "{}",
		DataType.Function => "(function)",
		_ => value.ToPrintString(),
	};

	private static IEnumerable<string> ScalarsOf(Table table)
	{
		for (var i = 1; i <= table.Length; i++)
		{
			var item = table.Get(i);
			if (item.Type == DataType.String || item.Type == DataType.Number || item.Type == DataType.Boolean)
				yield return item.ToPrintString();
			else
				yield return item.Type == DataType.Table ? "(table)" : item.ToPrintString();
		}
	}
}

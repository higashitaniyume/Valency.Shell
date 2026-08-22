using MoonSharp.Interpreter;

namespace Valency.Shell.Scripting.Lua;

/// <summary>
///     Object-mode command API: structured functions returning Lua-native values
///     (ls/cat/lines/writefile/grep/jobs). The argv/process layer (run/capture/pipe
///     and builtin commands) stays untouched alongside it.
/// </summary>
internal static class ObjectApi
{
	public static void Register(Script script, ILuaHost host)
	{
		var globals = script.Globals;
		globals.Set("ls", DynValue.NewCallback(new CallbackFunction(
			(ctx, args) => Ls(script, host, args), "ls")));
		globals.Set("cat", DynValue.NewCallback(new CallbackFunction(
			(ctx, args) => Cat(script, host, args), "cat")));
		globals.Set("lines", DynValue.NewCallback(new CallbackFunction(
			(ctx, args) => Lines(script, host, args), "lines")));
		globals.Set("writefile", DynValue.NewCallback(new CallbackFunction(
			(ctx, args) => WriteFile(script, host, args), "writefile")));
		globals.Set("grep", DynValue.NewCallback(new CallbackFunction(
			(ctx, args) => Grep(script, args), "grep")));
		globals.Set("jobs", DynValue.NewCallback(new CallbackFunction(
			(ctx, args) => Jobs(script, host), "jobs")));
	}

	// ---- 参数辅助 ----

	private static string PathArg(CallbackArguments args, ILuaHost host)
	{
		if (args.Count < 1 || args[0].Type != DataType.String)
			throw Errors.ArgString("path");
		return ResolvePath(args[0].String, host);
	}

	internal static string ResolvePath(string path, ILuaHost host)
	{
		var expanded = path.StartsWith('~')
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/', '\\'))
			: path;
		return Path.GetFullPath(expanded, host.CurrentDirectory);
	}

	private static void RequireFile(string path)
	{
		if (!File.Exists(path))
			throw new ScriptRuntimeException(string.Format(Resources.LuaFileNotFound, path));
	}

	// ---- ls ----

	private static DynValue Ls(Script script, ILuaHost host, CallbackArguments args)
	{
		var path = args.Count >= 1 && args[0].Type == DataType.String
			? ResolvePath(args[0].String, host)
			: host.CurrentDirectory;
		if (!Directory.Exists(path))
			throw new ScriptRuntimeException(string.Format(Resources.LuaDirNotFound, path));

		IEnumerable<FileSystemInfo> entries;
		try
		{
			var dir = new DirectoryInfo(path);
			entries = dir.EnumerateFileSystemInfos();
		}
		catch (Exception ex)
		{
			throw new ScriptRuntimeException(string.Format(Resources.LuaIoError, ex.Message));
		}

		var ordered = entries
			.OrderBy(e => e is DirectoryInfo ? 0 : 1)
			.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

		var result = DynValue.NewTable(script);
		var i = 0;
		foreach (var entry in ordered)
		{
			var item = DynValue.NewTable(script);
			item.Table.Set("name", DynValue.NewString(entry.Name));
			item.Table.Set("path", DynValue.NewString(entry.FullName));
			item.Table.Set("is_dir", DynValue.NewBoolean(entry is DirectoryInfo));
			item.Table.Set("mtime", DynValue.NewNumber(
				new DateTimeOffset(entry.LastWriteTimeUtc).ToUnixTimeSeconds()));
			if (entry is FileInfo file)
				item.Table.Set("size", DynValue.NewNumber(file.Length));
			result.Table.Set(DynValue.NewNumber(++i), item);
		}
		return result;
	}

	// ---- cat / lines / writefile ----

	private static DynValue Cat(Script script, ILuaHost host, CallbackArguments args)
	{
		var path = PathArg(args, host);
		RequireFile(path);
		try
		{
			return DynValue.NewString(File.ReadAllText(path));
		}
		catch (Exception ex)
		{
			throw new ScriptRuntimeException(string.Format(Resources.LuaIoError, ex.Message));
		}
	}

	private static DynValue Lines(Script script, ILuaHost host, CallbackArguments args)
	{
		var path = PathArg(args, host);
		RequireFile(path);
		try
		{
			return StringArray(script, File.ReadLines(path));
		}
		catch (Exception ex)
		{
			throw new ScriptRuntimeException(string.Format(Resources.LuaIoError, ex.Message));
		}
	}

	private static DynValue WriteFile(Script script, ILuaHost host, CallbackArguments args)
	{
		if (args.Count < 2 || args[0].Type != DataType.String || args[1].Type != DataType.String)
			throw Errors.ArgString("path, content");
		var path = ResolvePath(args[0].String, host);
		var append = args.Count >= 3 && args[2].Type == DataType.Boolean && args[2].Boolean;
		try
		{
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);
			if (append)
				File.AppendAllText(path, args[1].String);
			else
				File.WriteAllText(path, args[1].String);
			return DynValue.True;
		}
		catch (Exception ex)
		{
			throw new ScriptRuntimeException(string.Format(Resources.LuaIoError, ex.Message));
		}
	}

	// ---- grep（对象版：源是字符串或字符串数组，返回匹配行数组） ----

	private static DynValue Grep(Script script, CallbackArguments args)
	{
		if (args.Count < 1 || args[0].Type != DataType.String)
			throw Errors.ArgString("pattern, source");
		var pattern = args[0].String;

		var ignoreCase = false;
		if (args.Count >= 3 && args[2].Type == DataType.Table)
		{
			var flag = args[2].Table.Get("ignore_case");
			ignoreCase = flag.Type == DataType.Boolean && flag.Boolean;
		}
		var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		List<string> source;
		if (args.Count >= 2 && args[1].Type == DataType.String)
		{
			source = [.. args[1].String.Split(["\r\n", "\n", "\r"], StringSplitOptions.None)];
		}
		else if (args.Count >= 2 && args[1].Type == DataType.Table)
		{
			source = [];
			for (var i = 1; i <= args[1].Table.Length; i++)
			{
				var value = args[1].Table.Get(i);
				if (value.Type != DataType.String)
					throw new ScriptRuntimeException(Resources.LuaGrepSource);
				source.Add(value.String);
			}
		}
		else
		{
			throw new ScriptRuntimeException(Resources.LuaGrepSource);
		}

		return StringArray(script, source.Where(line => line.Contains(pattern, comparison)));
	}

	// ---- jobs（对象版：返回作业表；argv 版 jobs 内置命令保持打印） ----

	private static DynValue Jobs(Script script, ILuaHost host)
	{
		var result = DynValue.NewTable(script);
		var i = 0;
		foreach (var job in host.GetJobs())
		{
			var item = DynValue.NewTable(script);
			item.Table.Set("id", DynValue.NewNumber(job.Id));
			item.Table.Set("pid", DynValue.NewNumber(job.Pid));
			item.Table.Set("cmd", DynValue.NewString(job.Command));
			item.Table.Set("state", DynValue.NewString(job.State));
			result.Table.Set(DynValue.NewNumber(++i), item);
		}
		return result;
	}

	internal static DynValue StringArray(Script script, IEnumerable<string> items)
	{
		var result = DynValue.NewTable(script);
		var i = 0;
		foreach (var item in items)
			result.Table.Set(DynValue.NewNumber(++i), DynValue.NewString(item));
		return result;
	}
}

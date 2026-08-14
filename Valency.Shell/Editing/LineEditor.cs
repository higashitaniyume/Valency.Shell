using System.Text;
using Valency.Shell.Core.Highlighting;

namespace Valency.Shell.Editing;

public enum LineResultKind
{
    Command,
    Cancelled,
    Exit,
}

public readonly record struct LineResult(LineResultKind Kind, string Text);

public sealed class LineEditor
{
    private readonly Highlighter _highlighter = new();
    private readonly List<string> _history = new();
    private readonly StringBuilder _buffer = new();
    private int _cursor;
    private int _historyIndex;
    private string _savedLine = string.Empty;
    private string _prompt = string.Empty;
    private int _renderedLength;
    private int _startTop;
    private int _startLeft;
    private int _ansiCursorRow;

    public LineResult ReadLine(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            Console.Out.Write(prompt);
            var line = Console.In.ReadLine();
            if (line is null)
                return new LineResult(LineResultKind.Exit, string.Empty);
            return new LineResult(LineResultKind.Command, line);
        }

        _prompt = prompt;
        _buffer.Clear();
        _cursor = 0;
        _historyIndex = _history.Count;
        _savedLine = string.Empty;
        _renderedLength = 0;

        Console.TreatControlCAsInput = true;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                _startTop = Console.CursorTop;
                _startLeft = Console.CursorLeft;
            }
            _ansiCursorRow = 0;
            Console.Out.Write(prompt);
            Console.Out.Flush();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                var result = HandleKey(key);
                if (result.HasValue)
                {
                    Console.Out.WriteLine();
                    Console.Out.Flush();
                    return result.Value;
                }

                Render();
            }
        }
        finally
        {
            Console.TreatControlCAsInput = false;
        }
    }

    private LineResult? HandleKey(ConsoleKeyInfo key)
    {
        var ctrl = key.Modifiers.HasFlag(ConsoleModifiers.Control);

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                return Submit();

            case ConsoleKey.C when ctrl:
                return new LineResult(LineResultKind.Cancelled, string.Empty);

            case ConsoleKey.D when ctrl:
                if (_buffer.Length == 0)
                    return new LineResult(LineResultKind.Exit, string.Empty);
                DeleteAtCursor(1);
                return null;

            case ConsoleKey.L when ctrl:
                Console.Clear();
                _startTop = 0;
                _startLeft = 0;
                _ansiCursorRow = 0;
                Console.Out.Write(_prompt);
                _renderedLength = 0;
                return null;

            case ConsoleKey.A when ctrl:
                _cursor = 0;
                return null;
            case ConsoleKey.E when ctrl:
                _cursor = _buffer.Length;
                return null;

            case ConsoleKey.U when ctrl:
                _buffer.Remove(0, _cursor);
                _cursor = 0;
                return null;

            case ConsoleKey.K when ctrl:
                _buffer.Remove(_cursor, _buffer.Length - _cursor);
                return null;

            case ConsoleKey.W when ctrl:
                DeleteWordBackward();
                return null;

            case ConsoleKey.Escape:
                _buffer.Clear();
                _cursor = 0;
                return null;

            case ConsoleKey.Backspace when ctrl:
                DeleteWordBackward();
                return null;
            case ConsoleKey.Backspace:
                if (_cursor > 0)
                {
                    _buffer.Remove(_cursor - 1, 1);
                    _cursor--;
                }
                return null;

            case ConsoleKey.Delete when ctrl:
                DeleteWordForward();
                return null;
            case ConsoleKey.Delete:
                DeleteAtCursor(1);
                return null;

            case ConsoleKey.LeftArrow when ctrl:
                _cursor = WordStartBefore(_cursor);
                return null;
            case ConsoleKey.LeftArrow:
                if (_cursor > 0) _cursor--;
                return null;

            case ConsoleKey.RightArrow when ctrl:
                _cursor = WordEndAfter(_cursor);
                return null;
            case ConsoleKey.RightArrow:
                if (_cursor < _buffer.Length) _cursor++;
                return null;

            case ConsoleKey.Home:
                _cursor = 0;
                return null;
            case ConsoleKey.End:
                _cursor = _buffer.Length;
                return null;

            case ConsoleKey.UpArrow:
                MoveHistory(-1);
                return null;
            case ConsoleKey.DownArrow:
                MoveHistory(1);
                return null;

            default:
                if (!ctrl && key.KeyChar != '\0')
                {
                    _buffer.Insert(_cursor, key.KeyChar);
                    _cursor++;
                }
                return null;
        }
    }

    private LineResult Submit()
    {
        var text = _buffer.ToString();
        if (!string.IsNullOrWhiteSpace(text) && (_history.Count == 0 || _history[^1] != text))
            _history.Add(text);
        return new LineResult(LineResultKind.Command, text);
    }

    private void MoveHistory(int delta)
    {
        if (_history.Count == 0)
            return;

        var next = Math.Clamp(_historyIndex + delta, 0, _history.Count);
        if (next == _historyIndex)
            return;

        if (_historyIndex == _history.Count)
            _savedLine = _buffer.ToString();

        _historyIndex = next;

        var text = _historyIndex == _history.Count ? _savedLine : _history[_historyIndex];
        _buffer.Clear();
        _buffer.Append(text);
        _cursor = _buffer.Length;
    }

    private void DeleteAtCursor(int count)
    {
        if (_cursor >= _buffer.Length)
            return;
        _buffer.Remove(_cursor, Math.Min(count, _buffer.Length - _cursor));
    }

    private void DeleteWordBackward()
    {
        var start = WordStartBefore(_cursor);
        _buffer.Remove(start, _cursor - start);
        _cursor = start;
    }

    private void DeleteWordForward()
    {
        var end = WordEndAfter(_cursor);
        _buffer.Remove(_cursor, end - _cursor);
    }

    private int WordStartBefore(int pos)
    {
        var i = pos;
        while (i > 0 && char.IsWhiteSpace(_buffer[i - 1])) i--;
        while (i > 0 && !char.IsWhiteSpace(_buffer[i - 1])) i--;
        return i;
    }

    private int WordEndAfter(int pos)
    {
        var i = pos;
        while (i < _buffer.Length && char.IsWhiteSpace(_buffer[i])) i++;
        while (i < _buffer.Length && !char.IsWhiteSpace(_buffer[i])) i++;
        return i;
    }

    private void Render()
    {
        if (OperatingSystem.IsWindows())
            RenderWindows();
        else
            RenderAnsi();
        Console.Out.Flush();
    }

    private void RenderWindows()
    {
        var width = Console.BufferWidth > 0 ? Console.BufferWidth : 80;
        var height = Console.BufferHeight > 0 ? Console.BufferHeight : 24;

        Console.SetCursorPosition(_startLeft, _startTop);
        Console.Out.Write(_prompt);
        WriteHighlighted(_buffer.ToString());

        var total = _prompt.Length + _buffer.Length;
        if (_renderedLength > total)
            Console.Out.Write(new string(' ', _renderedLength - total));
        _renderedLength = total;

        var targetAbs = _startLeft + _prompt.Length + _cursor;
        var top = Math.Clamp(_startTop + targetAbs / width, 0, Math.Max(height - 1, 0));
        var left = targetAbs % width;
        Console.SetCursorPosition(left, top);
    }

    private void RenderAnsi()
    {
        var width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;

        // 回到提示符起始行（相对移动，不读取 Console.CursorTop，SSH/PTY 下其值不可靠）
        if (_ansiCursorRow > 0)
            Console.Out.Write($"\x1b[{_ansiCursorRow}A");
        Console.Out.Write('\r');

        Console.Out.Write(_prompt);
        WriteHighlighted(_buffer.ToString());

        // 清除光标到屏幕尾的残留（内容变短时抹掉旧字符）
        Console.Out.Write("\x1b[J");

        var total = _prompt.Length + _buffer.Length;
        var targetAbs = _prompt.Length + _cursor;
        var endRow = total / width;
        var targetRow = targetAbs / width;
        var targetCol = targetAbs % width;
        var curCol = total % width;

        if (endRow > targetRow)
            Console.Out.Write($"\x1b[{endRow - targetRow}A");
        if (curCol > targetCol)
            Console.Out.Write($"\x1b[{curCol - targetCol}D");
        else if (curCol < targetCol)
            Console.Out.Write($"\x1b[{targetCol - curCol}C");

        _ansiCursorRow = targetRow;
    }

    private void WriteHighlighted(string text)
    {
        var pos = 0;
        foreach (var span in _highlighter.Highlight(text))
        {
            if (span.Start > pos)
                Console.Out.Write(text[pos..span.Start]);
            Console.ForegroundColor = span.Color;
            Console.Out.Write(text.Substring(span.Start, span.Length));
            Console.ResetColor();
            pos = span.Start + span.Length;
        }
        if (pos < text.Length)
            Console.Out.Write(text[pos..]);
    }
}

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
            Console.Out.Write(prompt);
            _renderedLength = 0;

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                var result = HandleKey(key);
                if (result.HasValue)
                {
                    Console.Out.WriteLine();
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
        var width = Console.BufferWidth > 0 ? Console.BufferWidth
            : Console.WindowWidth > 0 ? Console.WindowWidth
            : 80;
        var height = Console.BufferHeight > 0 ? Console.BufferHeight
            : Console.WindowHeight > 0 ? Console.WindowHeight
            : 24;

        Console.Out.Write('\r');
        Console.Out.Write(_prompt);

        var text = _buffer.ToString();
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

        var total = _prompt.Length + _buffer.Length;
        if (_renderedLength > total)
            Console.Out.Write(new string(' ', _renderedLength - total));
        _renderedLength = Math.Max(_renderedLength, total);

        var endAbs = Console.CursorTop * width + Console.CursorLeft;
        var cursorAbs = endAbs - (_buffer.Length - _cursor);
        if (_renderedLength > total)
            cursorAbs -= _renderedLength - total;

        var top = Math.Clamp(cursorAbs / width, 0, Math.Max(height - 1, 0));
        var left = cursorAbs % width;
        Console.SetCursorPosition(left, top);
    }
}

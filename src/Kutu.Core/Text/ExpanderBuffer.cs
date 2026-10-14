using System.Text;
using Kutu.Core.Models;

namespace Kutu.Core.Text;

public sealed class ExpanderBuffer
{
    private readonly StringBuilder _buffer = new();

    public string Text => _buffer.ToString();

    public void Clear() => _buffer.Clear();

    public void Enter() => _buffer.Clear();

    public void Tab() => _buffer.Clear();

    public void Escape() => _buffer.Clear();

    public void FocusChanged() => _buffer.Clear();

    public void MouseClick() => _buffer.Clear();

    public void Backspace()
    {
        if (_buffer.Length > 0)
            _buffer.Length--;
    }

    public Snippet? Type(string? text, IEnumerable<Snippet> snippets, bool win = false, bool ctrl = false, bool alt = false)
    {
        if (win || ctrl || alt)
        {
            _buffer.Clear();
            return null;
        }

        if (string.IsNullOrEmpty(text))
            return null;

        Snippet? match = null;
        foreach (var ch in text)
        {
            if (ch is '\r' or '\n')
            {
                _buffer.Clear();
                continue;
            }

            if (char.IsControl(ch))
                continue;

            _buffer.Append(ch);
            if (_buffer.Length > 128)
                _buffer.Remove(0, _buffer.Length - 128);
            match = SnippetMatcher.Match(_buffer.ToString(), snippets);
            if (match is not null)
                return match;
        }

        return match;
    }
}

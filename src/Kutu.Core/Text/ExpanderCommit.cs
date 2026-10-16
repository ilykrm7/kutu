using Kutu.Core.Models;

namespace Kutu.Core.Text;

/// <summary>
/// Holds a match until the completing key is released, so every typed character
/// (including the first letter of a no-prefix trigger) is in the target before
/// backspaces are sent. Repeats of that key are swallowed.
/// </summary>
public sealed class ExpanderCommit
{
    private Snippet? _pending;
    private uint _vk;

    public bool HasPending => _pending is not null;

    public bool SwallowRepeat { get; private set; }

    public void KeyDown(uint virtualKey, Snippet? match)
    {
        SwallowRepeat = _pending is not null && virtualKey == _vk;
        if (SwallowRepeat || _pending is not null || match is null)
            return;

        var body = match.Body ?? "";
        var trigger = match.Trigger?.Trim() ?? "";
        if (body.Length == 0 || trigger.Length == 0)
            return;

        _pending = match;
        _vk = virtualKey;
    }

    public bool KeyUp(uint virtualKey, out Snippet? snippet, out int backspaces)
    {
        snippet = null;
        backspaces = 0;
        if (_pending is null || virtualKey != _vk)
            return false;

        snippet = _pending;
        backspaces = ExpanderReplacement.BackspaceCount(_pending.Trigger);
        _pending = null;
        return true;
    }

    public bool Flush(out Snippet? snippet, out int backspaces)
    {
        snippet = null;
        backspaces = 0;
        if (_pending is null)
            return false;

        snippet = _pending;
        backspaces = ExpanderReplacement.BackspaceCount(_pending.Trigger);
        _pending = null;
        SwallowRepeat = false;
        return true;
    }

    public void Cancel()
    {
        _pending = null;
        SwallowRepeat = false;
    }
}

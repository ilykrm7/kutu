namespace Kutu.Core.Hotkeys;

public readonly record struct HotkeyGesture(bool Control, bool Alt, bool Shift, bool Win, string Key)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Key);

    public override string ToString()
    {
        if (IsEmpty)
            return "";

        var parts = new List<string>(5);
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(NormalizeKey(Key));
        return string.Join('+', parts);
    }

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        bool control = false, alt = false, shift = false, win = false;
        string? key = null;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    control = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "win":
                case "windows":
                case "meta":
                    win = true;
                    break;
                default:
                    key = NormalizeKey(part);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(key))
            return false;

        gesture = new HotkeyGesture(control, alt, shift, win, key);
        return true;
    }

    public static string NormalizeKey(string key)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 1)
            return trimmed.ToUpperInvariant();

        return trimmed.ToLowerInvariant() switch
        {
            "space" or "spacebar" or "boşluk" or "bosluk" => "Space",
            "esc" or "escape" => "Escape",
            "enter" or "return" => "Enter",
            "tab" => "Tab",
            "printscreen" or "prtsc" or "snapshot" => "PrintScreen",
            "left" => "Left",
            "right" => "Right",
            "up" => "Up",
            "down" => "Down",
            _ => char.ToUpperInvariant(trimmed[0]) + trimmed[1..]
        };
    }
}

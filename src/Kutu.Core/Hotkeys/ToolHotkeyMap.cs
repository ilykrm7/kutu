using Kutu.Core.Models;
using Kutu.Core.Settings;

namespace Kutu.Core.Hotkeys;

public static class ToolHotkeyMap
{
    public const string LauncherId = "launcher";

    public const string LauncherDefault = "Alt+Space";

    public static readonly (string Id, string Default)[] Defaults =
    [
        ("screenshot", "Ctrl+Shift+S"),
        ("translate", "Ctrl+Alt+T"),
        ("screen-translate", "Ctrl+Alt+E"),
        ("screen-record", "Ctrl+Alt+R"),
        ("clipboard", "Ctrl+Alt+V"),
        ("ocr", "Ctrl+Alt+O"),
        ("color-picker", "Ctrl+Alt+C"),
        ("qr", "Ctrl+Alt+Q"),
        ("password", "Ctrl+Alt+P")
    ];

    public static Dictionary<string, string> Fresh()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, gesture) in Defaults)
            map[id] = gesture;
        return map;
    }

    public static string DefaultFor(string id)
    {
        foreach (var (known, gesture) in Defaults)
        {
            if (known.Equals(id, StringComparison.OrdinalIgnoreCase))
                return gesture;
        }

        return "";
    }

    public static string Get(AppSettings settings, string id)
    {
        if (id.Equals(LauncherId, StringComparison.OrdinalIgnoreCase))
            return settings.LauncherHotkey ?? "";

        if (settings.ToolHotkeys.TryGetValue(id, out var gesture))
            return gesture ?? "";

        return "";
    }

    public static void Set(AppSettings settings, string id, string? gesture)
    {
        var value = gesture?.Trim() ?? "";
        if (id.Equals(LauncherId, StringComparison.OrdinalIgnoreCase))
        {
            settings.LauncherHotkey = value;
            return;
        }

        settings.ToolHotkeys[id] = value;
        Mirror(settings);
    }

    public static void Reset(AppSettings settings, IEnumerable<string> toolIds)
    {
        settings.LauncherHotkey = LauncherDefault;
        settings.ToolHotkeys.Clear();
        foreach (var id in toolIds)
            settings.ToolHotkeys[id] = DefaultFor(id);
        Mirror(settings);
    }

    public static string Normalize(string? text)
    {
        if (!HotkeyGesture.TryParse(text, out var gesture))
            return "";

        return gesture.ToString();
    }

    public static IReadOnlyList<string> LocalConflicts(AppSettings settings, IEnumerable<string> toolIds)
    {
        var owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var blocked = new List<string>();
        void Consider(string id, string raw)
        {
            var key = Normalize(raw);
            if (key.Length == 0)
                return;

            if (owner.TryGetValue(key, out var other))
            {
                if (!blocked.Contains(id))
                    blocked.Add(id);
                if (!blocked.Contains(other))
                    blocked.Add(other);
                return;
            }

            owner[key] = id;
        }

        Consider(LauncherId, settings.LauncherHotkey);
        foreach (var id in toolIds)
            Consider(id, Get(settings, id));
        return blocked;
    }

    public static Dictionary<string, string> Import(SettingsDocument document)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (document.ToolHotkeys is not null)
        {
            foreach (var pair in document.ToolHotkeys)
                map[pair.Key] = pair.Value ?? "";
            foreach (var (id, fallback) in Defaults)
            {
                if (!map.ContainsKey(id))
                    map[id] = fallback;
            }

            return map;
        }

        var color = document.ColorPickerHotkey ?? "";
        if (Normalize(color).Equals(Normalize("Ctrl+Alt+P"), StringComparison.OrdinalIgnoreCase))
            color = "Ctrl+Alt+C";

        map["screenshot"] = document.ScreenshotHotkey ?? "";
        map["color-picker"] = color;
        map["clipboard"] = document.ClipboardHotkey ?? "";
        map["translate"] = document.TranslateHotkey ?? "";
        map["screen-translate"] = string.IsNullOrWhiteSpace(document.ScreenTranslateHotkey) ? "Ctrl+Alt+E" : document.ScreenTranslateHotkey;
        map["screen-record"] = string.IsNullOrWhiteSpace(document.RecordHotkey) ? "Ctrl+Alt+R" : document.RecordHotkey;
        foreach (var (id, fallback) in Defaults)
        {
            if (!map.ContainsKey(id))
                map[id] = fallback;
        }

        return map;
    }

    public static void Mirror(AppSettings settings)
    {
        settings.ScreenshotHotkey = Get(settings, "screenshot");
        settings.ColorPickerHotkey = Get(settings, "color-picker");
        settings.ClipboardHotkey = Get(settings, "clipboard");
        settings.TranslateHotkey = Get(settings, "translate");
        settings.ScreenTranslateHotkey = Get(settings, "screen-translate");
        settings.RecordHotkey = Get(settings, "screen-record");
    }
}

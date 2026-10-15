using Kutu.Core.Hotkeys;

namespace Kutu.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "tr";

    public bool StartWithWindows { get; set; }

    public bool ContextMenuRegistered { get; set; } = true;

    public string ScreenshotHotkey { get; set; } = "Ctrl+Shift+S";

    public string LauncherHotkey { get; set; } = "Alt+Space";

    public string ColorPickerHotkey { get; set; } = "Ctrl+Alt+C";

    public Dictionary<string, string> ToolHotkeys { get; set; } = ToolHotkeyMap.Fresh();

    public string ClipboardHotkey { get; set; } = "Ctrl+Alt+V";

    public string TranslateHotkey { get; set; } = "Ctrl+Alt+T";

    public string ScreenTranslateHotkey { get; set; } = "Ctrl+Alt+E";

    public string RecordHotkey { get; set; } = "Ctrl+Alt+R";

    public string ScreenTranslateTarget { get; set; } = "TR";

    public int ClipboardLimit { get; set; } = 50;

    public string UploadEndpoint { get; set; } = "https://kerimeksi.com/kutu/upload.php";

    public string DeepLEndpoint { get; set; } = "https://api-free.deepl.com/v2/translate";

    public string AbuseIpDbKey { get; set; } = "";

    public string VirusTotalKey { get; set; } = "";

    public string IpQualityScoreKey { get; set; } = "";

    public string OtxKey { get; set; } = "";

    public string GreyNoiseKey { get; set; } = "";

    public string ShodanKey { get; set; } = "";

    public string IpInfoKey { get; set; } = "";

    public string DeepLKey { get; set; } = "";

    public string UploadApiKey { get; set; } = "";

    public List<Snippet> Snippets { get; set; } = [];

    public List<ColorSwatch> ColorHistory { get; set; } = [];

    public int JpegQuality { get; set; } = 85;

    public string TranslateSource { get; set; } = "auto";

    public string TranslateTarget { get; set; } = "EN";

    public bool TextExpanderEnabled { get; set; } = true;

    public bool ExpanderDebug { get; set; }

    public bool ClipboardHistoryEnabled { get; set; } = true;

    public List<string> FavoriteTools { get; set; } = [];

    public int PasswordLength { get; set; } = 20;

    public bool PasswordUpper { get; set; } = true;

    public bool PasswordLower { get; set; } = true;

    public bool PasswordDigits { get; set; } = true;

    public bool PasswordSymbols { get; set; } = true;

    public bool PasswordExcludeAmbiguous { get; set; } = true;

    public bool CheckForUpdates { get; set; } = true;

    public bool AutoUpdate { get; set; }

    public string DismissedUpdateVersion { get; set; } = "";

    public string VideoOutputFolder { get; set; } = "";
}

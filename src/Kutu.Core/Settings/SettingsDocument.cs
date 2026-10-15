using Kutu.Core.Models;

namespace Kutu.Core.Settings;

public sealed class SettingsDocument
{
    public string Language { get; set; } = "tr";

    public bool StartWithWindows { get; set; }

    public bool ContextMenuRegistered { get; set; }

    public bool? ContextMenuUserSet { get; set; }

    public string ScreenshotHotkey { get; set; } = "Ctrl+Shift+S";

    public string LauncherHotkey { get; set; } = "Alt+Space";

    public string ColorPickerHotkey { get; set; } = "Ctrl+Alt+C";

    public Dictionary<string, string>? ToolHotkeys { get; set; }

    public string ClipboardHotkey { get; set; } = "Ctrl+Alt+V";

    public string TranslateHotkey { get; set; } = "Ctrl+Alt+T";

    public string ScreenTranslateHotkey { get; set; } = "Ctrl+Alt+E";

    public string RecordHotkey { get; set; } = "Ctrl+Alt+R";

    public string ScreenTranslateTarget { get; set; } = "TR";

    public int ClipboardLimit { get; set; } = 50;

    public string UploadEndpoint { get; set; } = "https://kerimeksi.com/kutu/upload.php";

    public string DeepLEndpoint { get; set; } = "https://api-free.deepl.com/v2/translate";

    public string AbuseIpDbKeyProtected { get; set; } = "";

    public string VirusTotalKeyProtected { get; set; } = "";

    public string IpQualityScoreKeyProtected { get; set; } = "";

    public string OtxKeyProtected { get; set; } = "";

    public string GreyNoiseKeyProtected { get; set; } = "";

    public string ShodanKeyProtected { get; set; } = "";

    public string IpInfoKeyProtected { get; set; } = "";

    public string DeepLKeyProtected { get; set; } = "";

    public string UploadApiKeyProtected { get; set; } = "";

    public List<Snippet> Snippets { get; set; } = [];

    public List<ColorSwatch> ColorHistory { get; set; } = [];

    public int JpegQuality { get; set; } = 85;

    public string TranslateSource { get; set; } = "auto";

    public string TranslateTarget { get; set; } = "EN";

    public bool? TextExpanderEnabled { get; set; }

    public bool ExpanderDebug { get; set; }

    public bool? ClipboardHistoryEnabled { get; set; }

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

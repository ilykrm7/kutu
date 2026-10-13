namespace Kutu.Core.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "tr";

    public bool StartWithWindows { get; set; }

    public bool ContextMenuRegistered { get; set; }

    public string ScreenshotHotkey { get; set; } = "Ctrl+Shift+S";

    public string LauncherHotkey { get; set; } = "Alt+Space";

    public string ColorPickerHotkey { get; set; } = "Ctrl+Alt+P";

    public string ClipboardHotkey { get; set; } = "Ctrl+Alt+V";

    public string TranslateHotkey { get; set; } = "Ctrl+Alt+T";

    public int ClipboardLimit { get; set; } = 50;

    public string UploadEndpoint { get; set; } = "https://kerimeksi.com/kutu/upload.php";

    public string DeepLEndpoint { get; set; } = "https://api-free.deepl.com/v2/translate";

    public string AbuseIpDbKey { get; set; } = "";

    public string DeepLKey { get; set; } = "";

    public string UploadApiKey { get; set; } = "";

    public List<Snippet> Snippets { get; set; } = [];

    public List<ColorSwatch> ColorHistory { get; set; } = [];

    public int JpegQuality { get; set; } = 85;

    public string TranslateSource { get; set; } = "auto";

    public string TranslateTarget { get; set; } = "TR";
}

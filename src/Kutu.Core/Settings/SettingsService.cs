using System.Text.Json;
using System.Text.Json.Serialization;
using Kutu.Core.Models;
using Kutu.Core.Security;

namespace Kutu.Core.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISecretProtector _protector;
    private readonly string _path;

    public SettingsService(ISecretProtector protector, string? path = null)
    {
        _protector = protector;
        _path = path ?? Path.Combine(AppDirectory, "settings.json");
    }

    public AppSettings Current { get; private set; } = new();

    public bool ContextMenuDirty { get; private set; }

    public bool ContextMenuExplicitOff { get; private set; }

    public static string AppDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kutu");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            Current = new AppSettings();
            ContextMenuDirty = true;
            ContextMenuExplicitOff = false;
            return;
        }

        SettingsDocument? document;
        try
        {
            using var stream = File.OpenRead(_path);
            document = await JsonSerializer.DeserializeAsync<SettingsDocument>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            document = null;
        }
        catch (IOException)
        {
            document = null;
        }
        if (document is null)
        {
            Current = new AppSettings();
            ContextMenuDirty = true;
            ContextMenuExplicitOff = false;
            return;
        }

        Current = FromDocument(document);
        ContextMenuExplicitOff = document.ContextMenuUserSet == true && !document.ContextMenuRegistered;
        ContextMenuDirty = !ContextMenuExplicitOff && !document.ContextMenuRegistered;
    }

    public void CorrectContextMenuOn()
    {
        if (!Current.ContextMenuRegistered)
            ContextMenuDirty = true;
        Current.ContextMenuRegistered = true;
        ContextMenuExplicitOff = false;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var document = ToDocument(Current);
        var temp = _path + ".tmp";
        using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (File.Exists(_path))
            File.Replace(temp, _path, null);
        else
            File.Move(temp, _path);
        ContextMenuDirty = false;
    }

    public SettingsDocument ToDocument(AppSettings settings) => new()
    {
        Language = settings.Language,
        StartWithWindows = settings.StartWithWindows,
        ContextMenuRegistered = settings.ContextMenuRegistered,
        ContextMenuUserSet = true,
        ScreenshotHotkey = settings.ScreenshotHotkey,
        LauncherHotkey = settings.LauncherHotkey,
        ColorPickerHotkey = settings.ColorPickerHotkey,
        ClipboardHotkey = settings.ClipboardHotkey,
        TranslateHotkey = settings.TranslateHotkey,
        ClipboardLimit = settings.ClipboardLimit,
        UploadEndpoint = settings.UploadEndpoint,
        DeepLEndpoint = settings.DeepLEndpoint,
        AbuseIpDbKeyProtected = Protect(settings.AbuseIpDbKey),
        VirusTotalKeyProtected = Protect(settings.VirusTotalKey),
        IpQualityScoreKeyProtected = Protect(settings.IpQualityScoreKey),
        OtxKeyProtected = Protect(settings.OtxKey),
        GreyNoiseKeyProtected = Protect(settings.GreyNoiseKey),
        ShodanKeyProtected = Protect(settings.ShodanKey),
        IpInfoKeyProtected = Protect(settings.IpInfoKey),
        DeepLKeyProtected = Protect(settings.DeepLKey),
        UploadApiKeyProtected = Protect(settings.UploadApiKey),
        Snippets = settings.Snippets,
        ColorHistory = settings.ColorHistory,
        JpegQuality = settings.JpegQuality,
        TranslateSource = settings.TranslateSource,
        TranslateTarget = settings.TranslateTarget,
        TextExpanderEnabled = settings.TextExpanderEnabled,
        ExpanderDebug = settings.ExpanderDebug,
        ClipboardHistoryEnabled = settings.ClipboardHistoryEnabled,
        FavoriteTools = settings.FavoriteTools
    };

    public AppSettings FromDocument(SettingsDocument document) => new()
    {
        Language = string.IsNullOrWhiteSpace(document.Language) ? "tr" : document.Language,
        StartWithWindows = document.StartWithWindows,
        ContextMenuRegistered = document.ContextMenuUserSet is null || document.ContextMenuRegistered,
        ScreenshotHotkey = document.ScreenshotHotkey,
        LauncherHotkey = document.LauncherHotkey,
        ColorPickerHotkey = document.ColorPickerHotkey,
        ClipboardHotkey = document.ClipboardHotkey,
        TranslateHotkey = document.TranslateHotkey,
        ClipboardLimit = document.ClipboardLimit <= 0 ? 50 : document.ClipboardLimit,
        UploadEndpoint = document.UploadEndpoint,
        DeepLEndpoint = string.IsNullOrWhiteSpace(document.DeepLEndpoint)
            ? "https://api-free.deepl.com/v2/translate"
            : document.DeepLEndpoint,
        AbuseIpDbKey = Unprotect(document.AbuseIpDbKeyProtected),
        VirusTotalKey = Unprotect(document.VirusTotalKeyProtected),
        IpQualityScoreKey = Unprotect(document.IpQualityScoreKeyProtected),
        OtxKey = Unprotect(document.OtxKeyProtected),
        GreyNoiseKey = Unprotect(document.GreyNoiseKeyProtected),
        ShodanKey = Unprotect(document.ShodanKeyProtected),
        IpInfoKey = Unprotect(document.IpInfoKeyProtected),
        DeepLKey = Unprotect(document.DeepLKeyProtected),
        UploadApiKey = Unprotect(document.UploadApiKeyProtected),
        Snippets = document.Snippets ?? [],
        ColorHistory = document.ColorHistory ?? [],
        JpegQuality = document.JpegQuality is < 1 or > 100 ? 85 : document.JpegQuality,
        TranslateSource = string.IsNullOrWhiteSpace(document.TranslateSource) ? "auto" : document.TranslateSource,
        TranslateTarget = string.IsNullOrWhiteSpace(document.TranslateTarget) ? "EN" : document.TranslateTarget,
        TextExpanderEnabled = document.TextExpanderEnabled ?? true,
        ExpanderDebug = document.ExpanderDebug,
        ClipboardHistoryEnabled = document.ClipboardHistoryEnabled ?? true,
        FavoriteTools = document.FavoriteTools ?? []
    };

    private string Protect(string plain) =>
        string.IsNullOrEmpty(plain) ? "" : _protector.Protect(plain);

    private string Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
            return "";

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch
        {
            return "";
        }
    }
}

using System.Windows;
using System.Windows.Media.Imaging;
using Kutu.Core.Models;
using Kutu.Core.Settings;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Tools.ClipboardHistory;
using Kutu.Tools.ColorPicker;
using Kutu.Tools.Screenshot;
using Kutu.Tools.Translate;
using Kutu.Tools.Upload;

namespace Kutu.App.Services;

public sealed class AppHost : IKutuHost
{
    private readonly ClipboardHistoryService _clipboard;
    private readonly Action _exit;
    private readonly Action _rebindHotkeys;
    private MainWindow? _window;
    private bool _restoreAfterCapture;

    public AppHost(SettingsService settings, ClipboardHistoryService clipboard, IReadOnlyList<IKutuTool> tools, Action exit, Action rebindHotkeys)
    {
        SettingsStore = settings;
        _clipboard = clipboard;
        Tools = tools;
        _exit = exit;
        _rebindHotkeys = rebindHotkeys;
    }

    public AppSettings Settings => SettingsStore.Current;

    public SettingsService SettingsStore { get; }

    public IClipboardHistory Clipboard => _clipboard;

    public IReadOnlyList<IKutuTool> Tools { get; }

    public void Attach(MainWindow window) => _window = window;

    public Task SaveSettingsAsync(CancellationToken cancellationToken = default) =>
        SettingsStore.SaveAsync(cancellationToken);

    public void Toast(string message) => _window?.ShowToast(message);

    public void CopyText(string text)
    {
        _clipboard.Suppress(TimeSpan.FromMilliseconds(500));
        System.Windows.Clipboard.SetText(text);
    }

    public void CopyImage(BitmapSource image)
    {
        _clipboard.Suppress(TimeSpan.FromMilliseconds(500));
        System.Windows.Clipboard.SetImage(image);
    }

    public async Task<string?> UploadAsync(byte[] data, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await UploadClient.UploadAsync(Settings.UploadEndpoint, Settings.UploadApiKey, data, fileName, contentType, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message == "missing-config")
        {
            Toast(L.Get("Upload_NeedConfig"));
            return null;
        }
    }

    public void SuppressClipboard(TimeSpan duration) => _clipboard.Suppress(duration);

    public void OpenTool(string toolId, object? parameter = null)
    {
        var tool = Tools.FirstOrDefault(item => item.Id == toolId);
        if (tool is null || _window is null)
            return;

        var view = tool.CreateView(this);
        if (parameter is not null && view is IAcceptsParameter accepts)
            accepts.SetParameter(parameter);

        _window.ShowTool(tool, view);
        ShowAndActivate();
    }

    public void ShowDashboard()
    {
        _window?.ShowDashboard();
        ShowAndActivate();
    }

    public void ShowSettings()
    {
        _window?.ShowSettings();
        ShowAndActivate();
    }

    public void ShowAndActivate()
    {
        if (_window is null)
            return;

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void HideForCapture()
    {
        _restoreAfterCapture = _window?.IsVisible == true;
        _window?.Hide();
    }

    public void RestoreAfterCapture()
    {
        if (_restoreAfterCapture)
            ShowAndActivate();
    }

    public void StartScreenshot() =>
        _window?.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                await ScreenshotController.StartAsync(this);
            }
            catch (Exception ex)
            {
                AppLog.Write(ex);
                Toast(ex.Message);
                RestoreAfterCapture();
            }
        });

    public void StartColorPicker()
    {
        try
        {
            ColorPickerController.Start(this);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            Toast(ex.Message);
        }
    }

    public void ShowClipboardPopup() => ClipboardPopup.Show(this);

    public void ShowTranslatePopup() => TranslatePopup.Show(this);

    public void ShowLauncher() => _window?.ToggleLauncher(this);

    public void RebindHotkeys() => _rebindHotkeys();

    public void Exit() => _exit();

    public void HandleArgs(string[] args)
    {
        if (args.Any(arg => arg.Equals("--screenshot", StringComparison.OrdinalIgnoreCase)))
        {
            StartScreenshot();
            return;
        }

        if (args.Any(arg => arg.Equals("--color", StringComparison.OrdinalIgnoreCase)))
        {
            StartColorPicker();
            return;
        }

        if (args.Any(arg => arg.Equals("--clipboard", StringComparison.OrdinalIgnoreCase)))
        {
            ShowClipboardPopup();
            return;
        }

        if (args.Any(arg => arg.Equals("--translate", StringComparison.OrdinalIgnoreCase)))
        {
            ShowTranslatePopup();
            return;
        }

        var files = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--convert", StringComparison.OrdinalIgnoreCase))
                continue;

            for (var j = i + 1; j < args.Length; j++)
            {
                if (args[j].StartsWith('-'))
                    break;
                files.Add(args[j].Trim('"'));
            }
        }

        if (files.Count > 0)
            OpenTool("image-converter", files);
    }
}

public static class AppLog
{
    public static void Write(Exception exception)
    {
        try
        {
            var dir = SettingsService.AppDirectory;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "kutu.log"), $"{DateTimeOffset.Now:u} {exception}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not crash the app.
        }
    }
}

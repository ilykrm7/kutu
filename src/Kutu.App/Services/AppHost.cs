using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Diagnostics;
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
    private Action<string>? _notifyTray;
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

    public void AttachTray(Action<string> notify) => _notifyTray = notify;

    public Task SaveSettingsAsync(CancellationToken cancellationToken = default) =>
        SettingsStore.SaveAsync(cancellationToken);

    public void Toast(string message) => _window?.ShowToast(message);

    public void NotifyTray(string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => NotifyTray(message));
            return;
        }

        if (_notifyTray is not null)
            _notifyTray(message);
        else
            Toast(message);
    }

    public void CopyText(string text)
    {
        _clipboard.Suppress(TimeSpan.FromMilliseconds(500));
        System.Windows.Clipboard.SetText(text);
    }

    public void CopyImage(BitmapSource image)
    {
        _clipboard.Suppress(TimeSpan.FromMilliseconds(800));
        var frozen = ToClipboardBitmap(image);
        Exception? last = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                var data = new DataObject();
                data.SetImage(frozen);
                System.Windows.Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (Exception ex) when (IsClipboardBusy(ex) && attempt < 7)
            {
                last = ex;
                Thread.Sleep(40 * (attempt + 1));
            }
            catch (Exception ex) when (IsClipboardBusy(ex))
            {
                last = ex;
            }
        }

        throw last ?? new ExternalException("CLIPBRD_E_CANT_OPEN", unchecked((int)0x800401D0));
    }

    public void Log(string message) => AppLog.Write(message);

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

    public void ShowTranslatePopup() => _ = SelectionTranslator.RunAsync(this);

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

        var convertTo = Array.FindIndex(args, arg => arg.Equals("--convert-to", StringComparison.OrdinalIgnoreCase));
        if (convertTo >= 0 && convertTo + 1 < args.Length)
        {
            var format = args[convertTo + 1];
            var targets = args.Skip(convertTo + 2).Where(arg => !arg.StartsWith("-", StringComparison.Ordinal)).Select(arg => arg.Trim('"')).ToList();
            Toast(ConvertCommand.Execute(format, targets, Settings.JpegQuality));
            return;
        }

        var files = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--convert", StringComparison.OrdinalIgnoreCase))
                continue;

            for (var j = i + 1; j < args.Length; j++)
            {
                if (args[j].StartsWith("-", StringComparison.Ordinal))
                    break;
                files.Add(args[j].Trim('"'));
            }
        }

        if (files.Count > 0)
            OpenTool("image-converter", files);
    }

    private static BitmapSource ToClipboardBitmap(BitmapSource image)
    {
        BitmapSource source = image;
        if (image.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = image;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            converted.Freeze();
            source = converted;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        frame.Freeze();
        return frame;
    }

    private static bool IsClipboardBusy(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (unchecked((uint)current.HResult) == 0x800401D0)
                return true;
        }

        return false;
    }
}

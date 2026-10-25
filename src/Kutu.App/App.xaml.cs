using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Kutu.App.Services;
using Kutu.Core;
using Kutu.Core.Diagnostics;
using Kutu.Core.Hotkeys;
using Kutu.Core.Network;
using Kutu.Core.Settings;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Tools.BulkIp;
using Kutu.Tools.ClipboardHistory;
using Kutu.Tools.ColorPicker;
using Kutu.Tools.Digest;
using Kutu.Tools.Fonts;
using Kutu.Tools.ImageConverter;
using Kutu.Tools.ImageResize;
using Kutu.Tools.IpReputation;
using Kutu.Tools.Matte;
using Kutu.Tools.NetProbe;
using Kutu.Tools.Ocr;
using Kutu.Tools.Password;
using Kutu.Tools.Qr;
using Kutu.Tools.ScreenRecord;
using Kutu.Tools.Screenshot;
using Kutu.Tools.ScreenTranslate;
using Kutu.Tools.SslCheck;
using Kutu.Tools.TextExpander;
using Kutu.Tools.Translate;
using Kutu.Tools.Upload;
using Kutu.Tools.Uuid;
using Kutu.Tools.VideoDownload;
using Kutu.Tools.Whois;
using Wpf.Ui.Appearance;

namespace Kutu.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private AppHost? _host;
    private MainWindow? _window;
    private TrayService? _tray;
    private GlobalHotkeyService? _hotkeys;
    private TextExpanderService? _expander;
    private CancellationTokenSource? _ipc;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TryDeleteOldExe();
        TlsPolicy.Enable();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherException;

        // Explorer starts one process per selected file. Those must convert
        // beside an already running Kutu and must not open a window.
        if (HeadlessConvert.TryStart(e.Args))
            return;

        _mutex = new Mutex(initiallyOwned: true, SingleInstance.MutexName, out var created);
        if (!created)
        {
            try
            {
                SingleInstance.Send(e.Args);
            }
            catch
            {
                // The primary instance may still be starting.
            }

            Shutdown();
            return;
        }

        try
        {
            var settings = new SettingsService(new DpapiProtector());
            settings.LoadAsync().GetAwaiter().GetResult();
            SyncContextMenu(settings);

            L.SetCulture(settings.Current.Language);
            ApplicationThemeManager.Apply(ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica, false);
            ApplicationAccentColorManager.Apply(Color.FromRgb(0x0A, 0x84, 0xFF), ApplicationTheme.Dark, false, false);

            var clipboard = new ClipboardHistoryService { Enabled = settings.Current.ClipboardHistoryEnabled };
            IKutuTool[] tools =
            [
                new ImageConverterTool(),
                new ImageResizeTool(),
                new MatteTool(),
                new ScreenshotTool(),
                new ColorPickerTool(),
                new FontsTool(),
                new ClipboardTool(),
                new TextExpanderTool(),
                new WhoisTool(),
                new IpReputationTool(),
                new BulkIpTool(),
                new NetProbeTool(),
                new SslCheckTool(),
                new UploadTool(),
                new TranslateTool(),
                new QrTool(),
                new OcrTool(),
                new ScreenTranslateTool(),
                new PasswordTool(),
                new UuidTool(),
                new DigestTool(),
                new VideoDownloadTool(),
                new ScreenRecordTool()
            ];
            _hotkeys = new GlobalHotkeyService();
            _host = new AppHost(settings, clipboard, tools, ExitApplication, BindHotkeys);
            _window = new MainWindow(_host);
            _host.Attach(_window);
            _window.SourceInitialized += (_, _) =>
            {
                _hotkeys.Attach(_window);
                clipboard.Start(_window);
                BindHotkeys();
            };
            _expander = new TextExpanderService(_host);
            _tray = new TrayService(_host);
            _host.AttachTray(_tray.ShowNotice);
            _ipc = new CancellationTokenSource();
            SingleInstance.StartServer(args => Dispatcher.Invoke(() => _host.HandleArgs(args)), _ipc.Token);
            _window.Show();
            _expander.Start();
            if (e.Args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
                _window.Hide();
            new UpdateController(_host, _window, ReleaseInstance).Start();
            _host.HandleArgs(e.Args);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            MessageBox.Show(ex.Message, "Kutu");
            Shutdown();
        }
    }

    private void BindHotkeys()
    {
        if (_host is null || _hotkeys is null)
            return;

        _hotkeys.Clear();
        var settings = _host.Settings;
        var ids = _host.Tools.Select(tool => tool.Id).ToList();
        var blocked = new HashSet<string>(ToolHotkeyMap.LocalConflicts(settings, ids), StringComparer.OrdinalIgnoreCase);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Take(string id, string gesture, Action action)
        {
            var key = ToolHotkeyMap.Normalize(gesture);
            if (key.Length == 0 || blocked.Contains(id) || !taken.Add(key))
                return;
            if (_hotkeys.Register(gesture, action))
                return;
            blocked.Add(id);
        }

        Take(ToolHotkeyMap.LauncherId, settings.LauncherHotkey, _host.ShowLauncher);
        foreach (var tool in _host.Tools)
            Take(tool.Id, ToolHotkeyMap.Get(settings, tool.Id), ActionFor(tool.Id));
        _host.HotkeysInUse = blocked;
    }

    private Action ActionFor(string id) => id switch
    {
        "screenshot" => _host!.StartScreenshot,
        "color-picker" => _host!.StartColorPicker,
        "clipboard" => _host!.ShowClipboardPopup,
        "translate" => _host!.ShowTranslatePopup,
        "screen-translate" => () => Observe(ScreenTranslateController.ToggleAsync(_host!)),
        "screen-record" => () => Observe(ScreenRecordController.ToggleAsync(_host!)),
        "ocr" => () =>
        {
            OcrLaunch.ReadScreen = true;
            _host!.OpenTool("ocr");
        },
        "qr" => () =>
        {
            QrLaunch.ReadScreen = true;
            _host!.OpenTool("qr");
        },
        _ => () => _host!.OpenTool(id)
    };

    private static async void Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
        }
    }

    private static void SyncContextMenu(SettingsService settings)
    {
        try
        {
            var exe = HostProcess.Executable;
            var staleOff = settings.ContextMenuExplicitOff && ContextMenuRegistration.HasClassicMenu();
            if (settings.ContextMenuExplicitOff && !staleOff)
            {
                ContextMenuRegistration.RemoveClassic();
            }
            else
            {
                if (!settings.Current.ContextMenuRegistered || staleOff)
                    settings.CorrectContextMenuOn();
                if (settings.Current.ContextMenuRegistered)
                    ContextMenuRegistration.EnsureClassic(exe);
            }

            if (settings.ContextMenuDirty)
                settings.SaveAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Menu registration must not stop startup or look like a crash.
        }
    }

    private void ReleaseInstance()
    {
        try
        {
            _ipc?.Cancel();
        }
        catch
        {
            // The pipe closes when the process exits.
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
            // The mutex is already released when startup did not own it.
        }
    }

    private static void TryDeleteOldExe()
    {
        try
        {
            var exe = HostProcess.Executable;
            var folder = string.IsNullOrWhiteSpace(exe) ? null : Path.GetDirectoryName(exe);
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;
            foreach (var path in Directory.GetFiles(folder, "Kutu.old*.exe"))
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // The previous process may still be exiting. The next start tries again.
                }
            }
        }
        catch
        {
            // Cleanup must not look like a crash.
        }
    }

    private void ExitApplication()
    {
        _ipc?.Cancel();
        _expander?.Dispose();
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _window?.ExitApplication();
        _mutex?.Dispose();
        Shutdown();
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Write(e.Exception);
        e.Handled = true;
    }
}

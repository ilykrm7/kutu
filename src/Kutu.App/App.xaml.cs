using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Kutu.App.Services;
using Kutu.Core;
using Kutu.Core.Diagnostics;
using Kutu.Core.Network;
using Kutu.Core.Settings;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Tools.ClipboardHistory;
using Kutu.Tools.ColorPicker;
using Kutu.Tools.ImageConverter;
using Kutu.Tools.ImageResize;
using Kutu.Tools.IpReputation;
using Kutu.Tools.Screenshot;
using Kutu.Tools.TextExpander;
using Kutu.Tools.Translate;
using Kutu.Tools.Upload;
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
            ApplicationAccentColorManager.Apply(SystemAccent(), ApplicationTheme.Dark, false, false);

            var clipboard = new ClipboardHistoryService { Enabled = settings.Current.ClipboardHistoryEnabled };
            IKutuTool[] tools =
            [
                new ImageConverterTool(),
                new ImageResizeTool(),
                new ScreenshotTool(),
                new ColorPickerTool(),
                new ClipboardTool(),
                new TextExpanderTool(),
                new WhoisTool(),
                new IpReputationTool(),
                new UploadTool(),
                new TranslateTool()
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
            if (settings.Current.ContextMenuRegistered)
            {
                var exe = HostProcess.Executable ?? "";
                _ = Task.Run(() =>
                {
                    try
                    {
                        ContextMenuRegistration.EnsureSparse(exe);
                    }
                    catch
                    {
                        // The classic menu is already registered.
                    }
                });
            }
            _ipc = new CancellationTokenSource();
            SingleInstance.StartServer(args => Dispatcher.Invoke(() => _host.HandleArgs(args)), _ipc.Token);
            _window.Show();
            _expander.Start();
            if (e.Args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
                _window.Hide();
            _host.HandleArgs(e.Args);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            MessageBox.Show(ex.Message, "Kutu");
            Shutdown();
        }
    }

    private static Color SystemAccent()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
                return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
        }
        catch
        {
            // Keep the brand accent when the registry value is missing.
        }

        return Color.FromRgb(0x28, 0xA0, 0xF1);
    }

    private void BindHotkeys()
    {
        if (_host is null || _hotkeys is null)
            return;

        _hotkeys.Clear();
        var settings = _host.Settings;
        Register(settings.LauncherHotkey, _host.ShowLauncher);
        Register(settings.ScreenshotHotkey, _host.StartScreenshot);
        Register(settings.ColorPickerHotkey, _host.StartColorPicker);
        Register(settings.ClipboardHotkey, _host.ShowClipboardPopup);
        Register(settings.TranslateHotkey, _host.ShowTranslatePopup);
    }

    private void Register(string gesture, Action action)
    {
        if (string.IsNullOrWhiteSpace(gesture))
            return;

        if (_hotkeys?.Register(gesture, action) == false)
            _host?.Toast(gesture);
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

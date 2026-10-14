using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kutu.App.Services;
using Kutu.Core.Hotkeys;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using PasswordBox = Wpf.Ui.Controls.PasswordBox;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.App.Views;

public sealed class SettingsView : UserControl
{
    private readonly IKutuHost _host;
    private readonly ToggleSwitch _startup;
    private readonly ToggleSwitch _contextMenu;
    private readonly ComboBox _language;
    private readonly TextBox _shot;
    private readonly TextBox _launcher;
    private readonly TextBox _color;
    private readonly TextBox _clipboard;
    private readonly TextBox _translate;
    private readonly TextBox _screen;
    private readonly TextBox _record;
    private readonly ComboBox _screenLang;
    private readonly ToggleSwitch _checkUpdates;
    private readonly ToggleSwitch _autoUpdate;
    private readonly TextBlock _version;
    private readonly StackPanel _updateNotice;
    private readonly TextBlock _updateNoticeText;
    private readonly TextBox _passwordLength;
    private readonly ToggleSwitch _passwordUpper;
    private readonly ToggleSwitch _passwordLower;
    private readonly ToggleSwitch _passwordDigits;
    private readonly ToggleSwitch _passwordSymbols;
    private readonly ToggleSwitch _passwordAmbiguous;
    private readonly PasswordBox _abuse;
    private readonly PasswordBox _virusTotal;
    private readonly PasswordBox _ipqs;
    private readonly PasswordBox _otx;
    private readonly PasswordBox _greyNoise;
    private readonly PasswordBox _shodan;
    private readonly PasswordBox _ipInfo;
    private readonly PasswordBox _deepL;
    private readonly PasswordBox _uploadKey;
    private readonly TextBox _deepLEndpoint;
    private readonly TextBox _uploadEndpoint;
    private readonly TextBlock _status;
    private bool _loading;

    public SettingsView(IKutuHost host)
    {
        _host = host;
        _startup = new ToggleSwitch { Content = L.Get("Settings_StartWithWindows") };
        _contextMenu = new ToggleSwitch { Content = L.Get("Settings_ContextMenu") };
        _language = new ComboBox { Width = 180, Height = 36 };
        _language.Items.Add(L.Get("Lang_Tr"));
        _language.Items.Add(L.Get("Lang_En"));
        _shot = HotkeyBox();
        _launcher = HotkeyBox();
        _color = HotkeyBox();
        _clipboard = HotkeyBox();
        _translate = HotkeyBox();
        _screen = HotkeyBox();
        _record = HotkeyBox();
        _screenLang = new ComboBox { Width = 180, Height = 36 };
        foreach (var code in TranslateDirection.Targets)
            _screenLang.Items.Add(code);
        _checkUpdates = new ToggleSwitch { Content = L.Get("Update_Check") };
        _autoUpdate = new ToggleSwitch { Content = L.Get("Update_Auto") };
        _version = Chrome.Hint("");
        _updateNoticeText = new TextBlock { Foreground = Chrome.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        var updateApply = Chrome.Primary(L.Get("Update_Action"), SymbolRegular.ArrowDownload24, (_, _) => UpdateController.Current?.Apply());
        _updateNotice = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed };
        _updateNotice.Children.Add(_updateNoticeText);
        _updateNotice.Children.Add(updateApply);
        _passwordLength = Chrome.Input("20");
        _passwordLength.Width = 88;
        _passwordUpper = Flag(L.Get("Pass_Upper"));
        _passwordLower = Flag(L.Get("Pass_Lower"));
        _passwordDigits = Flag(L.Get("Pass_Digits"));
        _passwordSymbols = Flag(L.Get("Pass_Symbols"));
        _passwordAmbiguous = Flag(L.Get("Pass_Ambiguous"));
        _abuse = new PasswordBox { RevealButtonEnabled = true };
        _virusTotal = new PasswordBox { RevealButtonEnabled = true };
        _ipqs = new PasswordBox { RevealButtonEnabled = true };
        _otx = new PasswordBox { RevealButtonEnabled = true };
        _greyNoise = new PasswordBox { RevealButtonEnabled = true };
        _shodan = new PasswordBox { RevealButtonEnabled = true };
        _ipInfo = new PasswordBox { RevealButtonEnabled = true };
        _deepL = new PasswordBox { RevealButtonEnabled = true };
        _uploadKey = new PasswordBox { RevealButtonEnabled = true };
        _deepLEndpoint = Chrome.Input("https://api-free.deepl.com/v2/translate");
        _uploadEndpoint = Chrome.Input("https://kerimeksi.com/kutu/upload.php");
        _status = Chrome.Hint("");
        _startup.Checked += async (_, _) => await ToggleStartupAsync(true);
        _startup.Unchecked += async (_, _) => await ToggleStartupAsync(false);
        _contextMenu.Checked += async (_, _) => await ToggleMenuAsync(true);
        _contextMenu.Unchecked += async (_, _) => await ToggleMenuAsync(false);
        _checkUpdates.Checked += async (_, _) => await PersistUpdateFlagsAsync();
        _checkUpdates.Unchecked += async (_, _) => await PersistUpdateFlagsAsync();
        _autoUpdate.Checked += async (_, _) => await PersistUpdateFlagsAsync();
        _autoUpdate.Unchecked += async (_, _) => await PersistUpdateFlagsAsync();
        if (UpdateController.Current is not null)
            UpdateController.Current.Changed += OnUpdatesChanged;

        var stack = new StackPanel();
        stack.Children.Add(Section(L.Get("Settings_General"), General()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Updates"), Updates()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Hotkeys"), Hotkeys()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_PassDefaults"), PasswordDefaults()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Api"), Api()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Integration"), Integration()));
        stack.Children.Add(Chrome.Primary(L.Get("Action_Save"), SymbolRegular.Save24, async (_, _) => await SaveAsync()));
        stack.Children.Add(_status);
        Content = Chrome.ScrollPage(stack, 760);
        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        _loading = true;
        var settings = _host.Settings;
        _startup.IsChecked = settings.StartWithWindows;
        _contextMenu.IsChecked = settings.ContextMenuRegistered;
        _language.SelectedIndex = settings.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _shot.Text = settings.ScreenshotHotkey;
        _launcher.Text = settings.LauncherHotkey;
        _color.Text = settings.ColorPickerHotkey;
        _clipboard.Text = settings.ClipboardHotkey;
        _translate.Text = settings.TranslateHotkey;
        _screen.Text = settings.ScreenTranslateHotkey;
        _record.Text = settings.RecordHotkey;
        _screenLang.SelectedItem = TranslateDirection.Targets.Contains(settings.ScreenTranslateTarget) ? settings.ScreenTranslateTarget : "TR";
        _checkUpdates.IsChecked = settings.CheckForUpdates;
        _autoUpdate.IsChecked = settings.AutoUpdate;
        _version.Text = L.Get("Update_Current") + "  v" + CurrentVersion();
        _passwordLength.Text = settings.PasswordLength.ToString();
        _passwordUpper.IsChecked = settings.PasswordUpper;
        _passwordLower.IsChecked = settings.PasswordLower;
        _passwordDigits.IsChecked = settings.PasswordDigits;
        _passwordSymbols.IsChecked = settings.PasswordSymbols;
        _passwordAmbiguous.IsChecked = settings.PasswordExcludeAmbiguous;
        RefreshUpdateNotice();
        _abuse.Password = settings.AbuseIpDbKey;
        _virusTotal.Password = settings.VirusTotalKey;
        _ipqs.Password = settings.IpQualityScoreKey;
        _otx.Password = settings.OtxKey;
        _greyNoise.Password = settings.GreyNoiseKey;
        _shodan.Password = settings.ShodanKey;
        _ipInfo.Password = settings.IpInfoKey;
        _deepL.Password = settings.DeepLKey;
        _uploadKey.Password = settings.UploadApiKey;
        _deepLEndpoint.Text = settings.DeepLEndpoint;
        _uploadEndpoint.Text = settings.UploadEndpoint;
        _loading = false;
    }

    private UIElement General()
    {
        var panel = new StackPanel();
        panel.Children.Add(_startup);
        panel.Children.Add(new Border { Height = 8 });
        panel.Children.Add(_contextMenu);
        panel.Children.Add(new Border { Height = 12 });
        panel.Children.Add(Chrome.Label(L.Get("Settings_Language")));
        panel.Children.Add(_language);
        panel.Children.Add(Chrome.Hint(L.Get("Settings_RestartHint")));
        return panel;
    }

    private UIElement Hotkeys()
    {
        var panel = new StackPanel();
        panel.Children.Add(Chrome.Hint(L.Get("Settings_HotkeyHint")));
        panel.Children.Add(Row(L.Get("Settings_ScreenshotHotkey"), _shot));
        panel.Children.Add(Row(L.Get("Settings_LauncherHotkey"), _launcher));
        panel.Children.Add(Row(L.Get("Settings_ColorHotkey"), _color));
        panel.Children.Add(Row(L.Get("Settings_ClipboardHotkey"), _clipboard));
        panel.Children.Add(Row(L.Get("Settings_TranslateHotkey"), _translate));
        panel.Children.Add(Row(L.Get("Settings_ScreenHotkey"), _screen));
        panel.Children.Add(Row(L.Get("Settings_RecordHotkey"), _record));
        panel.Children.Add(new Border { Height = 8 });
        panel.Children.Add(Chrome.Label(L.Get("Settings_ScreenLang")));
        panel.Children.Add(_screenLang);
        return panel;
    }

    private UIElement Updates()
    {
        var panel = new StackPanel();
        panel.Children.Add(_version);
        panel.Children.Add(new Border { Height = 8 });
        panel.Children.Add(_checkUpdates);
        panel.Children.Add(new Border { Height = 8 });
        panel.Children.Add(_autoUpdate);
        panel.Children.Add(new Border { Height = 12 });
        panel.Children.Add(Chrome.Secondary(L.Get("Update_Check"), SymbolRegular.ArrowDownload24, (_, _) => UpdateController.Current?.CheckNow()));
        panel.Children.Add(_updateNotice);
        return panel;
    }

    private UIElement PasswordDefaults()
    {
        var panel = new StackPanel();
        panel.Children.Add(Chrome.Label(L.Get("Pass_Length")));
        panel.Children.Add(_passwordLength);
        panel.Children.Add(_passwordUpper);
        panel.Children.Add(_passwordLower);
        panel.Children.Add(_passwordDigits);
        panel.Children.Add(_passwordSymbols);
        panel.Children.Add(_passwordAmbiguous);
        return panel;
    }

    private UIElement Api()
    {
        var panel = new StackPanel();
        panel.Children.Add(Chrome.Hint(L.Get("Settings_KeysHint")));
        panel.Children.Add(Chrome.Label(L.Get("Settings_AbuseKey")));
        panel.Children.Add(_abuse);
        panel.Children.Add(KeyRow(L.Get("Settings_VirusTotalKey"), _virusTotal));
        panel.Children.Add(KeyRow(L.Get("Settings_IpqsKey"), _ipqs));
        panel.Children.Add(KeyRow(L.Get("Settings_OtxKey"), _otx));
        panel.Children.Add(KeyRow(L.Get("Settings_GreyNoiseKey"), _greyNoise));
        panel.Children.Add(KeyRow(L.Get("Settings_ShodanKey"), _shodan));
        panel.Children.Add(KeyRow(L.Get("Settings_IpInfoKey"), _ipInfo));
        panel.Children.Add(Chrome.Label(L.Get("Settings_DeepLKey")));
        panel.Children.Add(_deepL);
        panel.Children.Add(Chrome.Label(L.Get("Settings_DeepLEndpoint")));
        panel.Children.Add(_deepLEndpoint);
        return panel;
    }

    private UIElement Integration()
    {
        var panel = new StackPanel();
        panel.Children.Add(Chrome.Label(L.Get("Settings_UploadEndpoint")));
        panel.Children.Add(_uploadEndpoint);
        panel.Children.Add(Chrome.Label(L.Get("Settings_UploadKey")));
        panel.Children.Add(_uploadKey);
        return panel;
    }

    private async Task ToggleStartupAsync(bool enabled)
    {
        if (_loading)
            return;

        try
        {
            StartupRegistration.SetEnabled(enabled);
            _host.Settings.StartWithWindows = enabled;
            await _host.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private async Task ToggleMenuAsync(bool enabled)
    {
        if (_loading)
            return;

        try
        {
            var note = ContextMenuRegistration.SetEnabled(enabled);
            _host.Settings.ContextMenuRegistered = enabled;
            await _host.SaveSettingsAsync();
            if (note == "classic-only")
                _status.Text = L.Get("Menu_ModernFail");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private async Task SaveAsync()
    {
        var settings = _host.Settings;
        var language = _language.SelectedIndex == 1 ? "en" : "tr";
        var languageChanged = !settings.Language.Equals(language, StringComparison.OrdinalIgnoreCase);
        settings.Language = language;
        settings.ScreenshotHotkey = _shot.Text.Trim();
        settings.LauncherHotkey = _launcher.Text.Trim();
        settings.ColorPickerHotkey = _color.Text.Trim();
        settings.ClipboardHotkey = _clipboard.Text.Trim();
        settings.TranslateHotkey = _translate.Text.Trim();
        settings.ScreenTranslateHotkey = _screen.Text.Trim();
        settings.RecordHotkey = _record.Text.Trim();
        settings.ScreenTranslateTarget = _screenLang.SelectedItem as string ?? "TR";
        settings.CheckForUpdates = _checkUpdates.IsChecked == true;
        settings.AutoUpdate = _autoUpdate.IsChecked == true;
        if (!int.TryParse(_passwordLength.Text.Trim(), out var length) || length is < 4 or > 128)
            length = 20;
        settings.PasswordLength = length;
        settings.PasswordUpper = _passwordUpper.IsChecked == true;
        settings.PasswordLower = _passwordLower.IsChecked == true;
        settings.PasswordDigits = _passwordDigits.IsChecked == true;
        settings.PasswordSymbols = _passwordSymbols.IsChecked == true;
        settings.PasswordExcludeAmbiguous = _passwordAmbiguous.IsChecked == true;
        settings.AbuseIpDbKey = _abuse.Password.Trim();
        settings.VirusTotalKey = _virusTotal.Password.Trim();
        settings.IpQualityScoreKey = _ipqs.Password.Trim();
        settings.OtxKey = _otx.Password.Trim();
        settings.GreyNoiseKey = _greyNoise.Password.Trim();
        settings.ShodanKey = _shodan.Password.Trim();
        settings.IpInfoKey = _ipInfo.Password.Trim();
        settings.DeepLKey = _deepL.Password.Trim();
        settings.UploadApiKey = _uploadKey.Password.Trim();
        settings.DeepLEndpoint = _deepLEndpoint.Text.Trim();
        settings.UploadEndpoint = _uploadEndpoint.Text.Trim();
        await _host.SaveSettingsAsync();
        _host.RebindHotkeys();
        UpdateController.Current?.EnsureWatching();
        L.SetCulture(settings.Language);
        _status.Text = languageChanged ? L.Get("Settings_RestartHint") : L.Get("Settings_Saved");
        _host.Toast(_status.Text);
    }

    private async Task PersistUpdateFlagsAsync()
    {
        if (_loading)
            return;
        _host.Settings.CheckForUpdates = _checkUpdates.IsChecked == true;
        _host.Settings.AutoUpdate = _autoUpdate.IsChecked == true;
        await _host.SaveSettingsAsync();
        UpdateController.Current?.EnsureWatching();
    }

    private void OnUpdatesChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(RefreshUpdateNotice));
            return;
        }

        RefreshUpdateNotice();
    }

    private void RefreshUpdateNotice()
    {
        var version = UpdateController.Current?.Available;
        if (string.IsNullOrWhiteSpace(version))
        {
            _updateNotice.Visibility = Visibility.Collapsed;
            return;
        }

        _updateNoticeText.Text = string.Format(L.Get("Update_Available"), version);
        _updateNotice.Visibility = Visibility.Visible;
    }

    private static string CurrentVersion()
    {
        var version = typeof(SettingsView).Assembly.GetName().Version;
        return version is null ? "1.1.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static ToggleSwitch Flag(string label) => new() { Content = label, Margin = new Thickness(0, 6, 0, 0) };

    private static TextBox HotkeyBox()
    {
        var box = Chrome.Input("Ctrl+Shift+S");
        box.Width = 220;
        box.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            if (e.Key is Key.Escape or Key.Delete or Key.Back)
            {
                box.Text = "";
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return;

            var mods = Keyboard.Modifiers;
            var name = key == Key.Snapshot ? "PrintScreen" : key.ToString();
            var gesture = new HotkeyGesture(
                mods.HasFlag(ModifierKeys.Control),
                mods.HasFlag(ModifierKeys.Alt),
                mods.HasFlag(ModifierKeys.Shift),
                mods.HasFlag(ModifierKeys.Windows),
                name);
            box.Text = gesture.ToString();
        };
        return box;
    }

    private static UIElement Section(string title, UIElement body)
    {
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Heading(title));
        stack.Children.Add(body);
        return Chrome.Card(stack);
    }

    private static UIElement KeyRow(string label, UIElement input)
    {
        var panel = new StackPanel();
        panel.Children.Add(Chrome.Label(label));
        panel.Children.Add(input);
        return panel;
    }

    private static UIElement Row(string label, UIElement input)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var text = new TextBlock { Text = label, Foreground = Chrome.Text, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(input, 1);
        grid.Children.Add(text);
        grid.Children.Add(input);
        return grid;
    }

    private static Border Gap() => new() { Height = 14 };
}

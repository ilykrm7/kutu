using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kutu.App.Services;
using Kutu.Core.Hotkeys;
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
    private readonly PasswordBox _abuse;
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
        _abuse = new PasswordBox { RevealButtonEnabled = true };
        _deepL = new PasswordBox { RevealButtonEnabled = true };
        _uploadKey = new PasswordBox { RevealButtonEnabled = true };
        _deepLEndpoint = Chrome.Input("https://api-free.deepl.com/v2/translate");
        _uploadEndpoint = Chrome.Input("https://kerimeksi.com/kutu/upload.php");
        _status = Chrome.Hint("");
        _startup.Checked += async (_, _) => await ToggleStartupAsync(true);
        _startup.Unchecked += async (_, _) => await ToggleStartupAsync(false);
        _contextMenu.Checked += async (_, _) => await ToggleMenuAsync(true);
        _contextMenu.Unchecked += async (_, _) => await ToggleMenuAsync(false);

        var stack = new StackPanel();
        stack.Children.Add(Section(L.Get("Settings_General"), General()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Hotkeys"), Hotkeys()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Api"), Api()));
        stack.Children.Add(Gap());
        stack.Children.Add(Section(L.Get("Settings_Integration"), Integration()));
        stack.Children.Add(Chrome.Primary(L.Get("Action_Save"), SymbolRegular.Save24, async (_, _) => await SaveAsync()));
        stack.Children.Add(_status);
        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(28, 18, 28, 28), Child = stack },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
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
        _abuse.Password = settings.AbuseIpDbKey;
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
        return panel;
    }

    private UIElement Api()
    {
        var panel = new StackPanel();
        panel.Children.Add(Chrome.Hint(L.Get("Settings_KeysHint")));
        panel.Children.Add(Chrome.Label(L.Get("Settings_AbuseKey")));
        panel.Children.Add(_abuse);
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
            ContextMenuRegistration.SetEnabled(enabled);
            _host.Settings.ContextMenuRegistered = enabled;
            await _host.SaveSettingsAsync();
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
        settings.AbuseIpDbKey = _abuse.Password.Trim();
        settings.DeepLKey = _deepL.Password.Trim();
        settings.UploadApiKey = _uploadKey.Password.Trim();
        settings.DeepLEndpoint = _deepLEndpoint.Text.Trim();
        settings.UploadEndpoint = _uploadEndpoint.Text.Trim();
        await _host.SaveSettingsAsync();
        _host.RebindHotkeys();
        L.SetCulture(settings.Language);
        _status.Text = languageChanged ? L.Get("Settings_RestartHint") : L.Get("Settings_Saved");
        _host.Toast(_status.Text);
    }

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

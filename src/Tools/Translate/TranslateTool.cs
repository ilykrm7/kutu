using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Translate;

public sealed class TranslateTool : IKutuTool
{
    public string Id => "translate";

    public string TitleKey => "Tool_Translate_Title";

    public string DescriptionKey => "Tool_Translate_Desc";

    public string Symbol => "Translate24";

    public IReadOnlyList<string> Aliases { get; } = ["çevir", "cevir", "translate", "tr", "deepl", "çeviri", "ceviri"];

    public int Order => 100;

    public UIElement CreateView(IKutuHost host) => new TranslateView(host);
}

public sealed class TranslateView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _input;
    private readonly TextBox _output;
    private readonly TextBlock _status;

    public TranslateView(IKutuHost host)
    {
        _host = host;
        _input = Chrome.Input(L.Get("Translate_Input"), multiline: true);
        _output = Chrome.Input(L.Get("Translate_Output"), multiline: true);
        _output.IsReadOnly = true;
        _status = Chrome.Hint("");
        var target = LanguageBox.Create(host.Settings.ScreenTranslateTarget, code => TranslatePopup.Remember(host, code));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Translate"), SymbolRegular.Translate24, async (_, _) => await RunAsync(clipboard: false)));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_TranslateClipboard"), SymbolRegular.ClipboardPaste24, async (_, _) => await RunAsync(clipboard: true)));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_output.Text))
            {
                host.CopyText(_output.Text);
                host.Toast(L.Get("Action_Copied"));
            }
        }));

        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Translate_Sticky")));
        stack.Children.Add(Chrome.Label(L.Get("Translate_Target")));
        stack.Children.Add(target);
        stack.Children.Add(new Border { Height = 8 });
        stack.Children.Add(Chrome.Label(L.Get("Translate_Input")));
        stack.Children.Add(_input);
        stack.Children.Add(buttons);
        stack.Children.Add(Chrome.Label(L.Get("Translate_Output")));
        stack.Children.Add(_output);
        stack.Children.Add(_status);
        Content = stack;
    }

    private async Task RunAsync(bool clipboard)
    {
        try
        {
            var text = clipboard ? ReadClipboard() : _input.Text;
            if (clipboard)
                _input.Text = text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _status.Text = L.Get("Translate_Empty");
                return;
            }

            _status.Text = "";
            var result = await TranslateClient.TranslateAsync(text, "auto", TranslatePopup.Target(_host), _host.Settings.DeepLKey, _host.Settings.DeepLEndpoint);
            _output.Text = result.Text;
            _status.Text = string.IsNullOrWhiteSpace(result.Provider) ? "" : string.Format(L.Get("Translate_Provider"), result.Provider);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message == "translate-failed" ? L.Get("Translate_Failed") : ex.Message;
        }
    }

    private static string ReadClipboard()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : "";
        }
        catch
        {
            return "";
        }
    }

}

public static class TranslatePopup
{
    public static void Show(IKutuHost host)
    {
        var text = "";
        try
        {
            if (Clipboard.ContainsText())
                text = Clipboard.GetText();
        }
        catch
        {
            // Clipboard can be busy.
        }

        var window = new Window
        {
            Width = 560,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };

        var target = LanguageBox.Create(host.Settings.ScreenTranslateTarget, code => TranslatePopup.Remember(host, code));
        var input = Chrome.Input(L.Get("Translate_PopupHint"), multiline: true);
        input.Text = text;
        input.MinHeight = 90;
        var output = Chrome.Input(L.Get("Translate_Output"), multiline: true);
        output.IsReadOnly = true;
        output.MinHeight = 90;
        var status = Chrome.Hint("");
        var card = new StackPanel();
        card.Children.Add(Chrome.Heading(L.Get("Tool_Translate_Title")));
        card.Children.Add(Chrome.Label(L.Get("Translate_Target")));
        card.Children.Add(target);
        card.Children.Add(new Border { Height = 8 });
        card.Children.Add(input);
        card.Children.Add(new Border { Height = 8 });
        card.Children.Add(output);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Translate"), SymbolRegular.Translate24, async (_, _) =>
        {
            try
            {
                status.Text = L.Get("Common_Working");
                if (string.IsNullOrWhiteSpace(input.Text))
                {
                    status.Text = L.Get("Translate_Empty");
                    return;
                }

                var result = await TranslateClient.TranslateAsync(
                    input.Text,
                    "auto",
                    Target(host),
                    host.Settings.DeepLKey,
                    host.Settings.DeepLEndpoint);
                output.Text = result.Text;
                status.Text = string.IsNullOrWhiteSpace(result.Provider) ? "" : string.Format(L.Get("Translate_Provider"), result.Provider);
            }
            catch (Exception ex)
            {
                status.Text = ex.Message == "translate-failed" ? L.Get("Translate_Failed") : ex.Message;
            }
        }));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(output.Text))
                host.CopyText(output.Text);
        }));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Close"), SymbolRegular.Dismiss24, (_, _) => window.Close()));
        card.Children.Add(buttons);
        card.Children.Add(status);
        window.Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18),
            Child = card
        };
        window.Deactivated += (_, _) => window.Close();
        window.Show();
        input.Focus();
    }

    public static string Target(IKutuHost host) => TranslateDirection.RememberedTarget(host.Settings.ScreenTranslateTarget);

    public static async void Remember(IKutuHost host, string code)
    {
        var next = TranslateDirection.RememberedTarget(code);
        if (string.Equals(host.Settings.ScreenTranslateTarget, next, StringComparison.OrdinalIgnoreCase))
            return;

        host.Settings.ScreenTranslateTarget = next;
        try
        {
            await host.SaveSettingsAsync();
        }
        catch (Exception ex)
        {
            host.Log(ex.ToString());
        }
    }
}

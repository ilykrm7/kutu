using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Kutu.Tools.Ocr;
using Kutu.Tools.Screenshot;
using Kutu.Tools.Translate;
using Wpf.Ui.Controls;

namespace Kutu.Tools.ScreenTranslate;

public sealed class ScreenTranslateTool : IKutuTool
{
    public string Id => "screen-translate";

    public string TitleKey => "Tool_Screen_Title";

    public string DescriptionKey => "Tool_Screen_Desc";

    public string Symbol => "Translate24";

    public IReadOnlyList<string> Aliases { get; } = ["ekran çevir", "ekran cevir", "screen", "overlay", "çeviri ekran"];

    public int Order => 108;

    public UIElement CreateView(IKutuHost host) => new ScreenTranslateView(host);
}

public static class ScreenTranslateController
{
    public static async Task StartAsync(IKutuHost host)
    {
        host.HideForCapture();
        RegionPick? pick;
        try
        {
            await Task.Delay(160);
            pick = RegionOverlay.Choose(true, L.Get("Action_FullScreen"));
        }
        finally
        {
            host.RestoreAfterCapture();
        }

        if (pick is null)
            return;

        OcrPage page;
        try
        {
            page = await WindowsOcr.ReadAsync(pick.Bitmap);
        }
        catch (Exception ex)
        {
            host.Toast(ex.Message == "ocr-missing" ? L.Get("Ocr_Missing") : ex.Message);
            return;
        }

        var target = string.IsNullOrWhiteSpace(host.Settings.ScreenTranslateTarget) ? "TR" : host.Settings.ScreenTranslateTarget;
        var texts = page.Lines.Select(line => line.Text).ToList();
        var marked = ScreenLinePlan.Mark(texts, target);
        var pending = ScreenLinePlan.Pending(marked).ToList();
        var translated = new List<string>();
        for (var offset = 0; offset < pending.Count; offset += 20)
        {
            var slice = pending.Skip(offset).Take(20).ToList();
            var packed = ScreenLinePlan.Pack(slice, slice.Count)[0];
            var result = await TranslateClient.TranslateAsync(packed, "auto", target, host.Settings.DeepLKey, host.Settings.DeepLEndpoint);
            var parts = ScreenLinePlan.Unpack(result.Text, slice.Count);
            if (parts.All(string.IsNullOrWhiteSpace) && slice.Count > 1)
            {
                for (var i = 0; i < slice.Count; i++)
                {
                    var single = await TranslateClient.TranslateAsync(slice[i], "auto", target, host.Settings.DeepLKey, host.Settings.DeepLEndpoint);
                    parts[i] = single.Text;
                }
            }

            translated.AddRange(parts);
        }

        var pairs = new List<(OcrLineBox Line, string Translation)>();
        var cursor = 0;
        for (var i = 0; i < marked.Length; i++)
        {
            var translation = marked[i].Skip ? "" : (cursor < translated.Count ? translated[cursor++] : "");
            pairs.Add((page.Lines[i], translation));
        }

        ShowOverlay(pick, pairs);
    }

    private static void ShowOverlay(RegionPick pick, IReadOnlyList<(OcrLineBox Line, string Translation)> pairs)
    {
        var dpiX = pick.Bitmap.DpiX <= 1 ? 96 : pick.Bitmap.DpiX;
        var dpiY = pick.Bitmap.DpiY <= 1 ? 96 : pick.Bitmap.DpiY;
        var scaleX = dpiX / 96d;
        var scaleY = dpiY / 96d;
        var canvas = new Canvas();
        var copy = new StringBuilder();
        foreach (var (line, translation) in pairs)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
                continue;
            copy.AppendLine(line.Text);
            if (!string.IsNullOrWhiteSpace(translation))
            {
                copy.AppendLine(translation);
                copy.AppendLine();
            }

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = line.Text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC5, 0xD0, 0xE0)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrWhiteSpace(translation))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = translation,
                    Foreground = Brushes.White,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE8, 0x10, 0x18, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0xA0, 0xF1)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 6),
                MaxWidth = 420,
                Child = stack
            };
            Canvas.SetLeft(card, line.X / scaleX);
            Canvas.SetTop(card, (line.Y + line.Height) / scaleY);
            canvas.Children.Add(card);
        }

        var copyText = copy.ToString().Trim();
        var button = Chrome.Secondary(L.Get("Action_CopyAll"), SymbolRegular.Copy24, (_, _) =>
        {
            try
            {
                Clipboard.SetText(copyText);
            }
            catch
            {
                // The clipboard can be busy.
            }
        });
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12) };
        bar.Children.Add(button);
        var root = new Grid();
        root.Children.Add(new Image { Source = pick.Bitmap, Stretch = Stretch.Fill });
        root.Children.Add(canvas);
        root.Children.Add(bar);
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Left = pick.ScreenX / scaleX,
            Top = pick.ScreenY / scaleY,
            Width = Math.Max(120, pick.Width / scaleX),
            Height = Math.Max(80, pick.Height / scaleY),
            Content = root
        };
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                window.Close();
        };
        window.Show();
        window.Activate();
        window.Focus();
    }
}

public sealed class ScreenTranslateView : UserControl
{
    public ScreenTranslateView(IKutuHost host)
    {
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Screen_Desc")));
        stack.Children.Add(Chrome.Hint(string.Format(L.Get("Dash_Hint"), host.Settings.ScreenTranslateHotkey)));
        stack.Children.Add(Chrome.Hint(L.Get("Screen_Target") + ": " + host.Settings.ScreenTranslateTarget));
        stack.Children.Add(Chrome.Primary(L.Get("Action_ReadScreen"), SymbolRegular.Translate24, async (_, _) => await ScreenTranslateController.StartAsync(host)));
        Content = stack;
    }
}

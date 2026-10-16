using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Geometry;
using Kutu.Core.Hotkeys;
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
    private const int EscHotkeyId = 0x4B11;
    private const int WmHotkey = 0x0312;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private static Window? _overlay;

    public static bool OverlayOpen => _overlay is { IsVisible: true };

    public static Task ToggleAsync(IKutuHost host)
    {
        if (OverlayOpen)
        {
            CloseOverlay();
            return Task.CompletedTask;
        }

        return StartAsync(host);
    }

    public static void CloseOverlay()
    {
        var window = _overlay;
        if (window is null)
            return;

        _overlay = null;
        if (window.Dispatcher.CheckAccess())
            window.Close();
        else
            window.Dispatcher.Invoke(window.Close);
    }

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
        try
        {
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
        }
        catch (Exception ex)
        {
            host.Toast(ex.Message == "translate-failed" ? L.Get("Translate_Failed") : ex.Message);
            return;
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
        var dipWidth = pick.DipWidth > 1 ? pick.DipWidth : Math.Max(120, pick.Width);
        var dipHeight = pick.DipHeight > 1 ? pick.DipHeight : Math.Max(80, pick.Height);
        var canvas = new Canvas();
        var copy = new StringBuilder();
        foreach (var (line, translation) in pairs)
        {
            if (!ScreenLinePlan.ShowTranslation(line.Text, translation))
                continue;
            copy.AppendLine(line.Text.Trim());
            copy.AppendLine(translation.Trim());
            copy.AppendLine();
            var box = TranslationBox(pick, line, dipWidth, dipHeight);
            var maxWidth = Math.Max(72, Math.Min(box.Width, Math.Max(72, dipWidth - box.X - 8)));
            var top = box.Y;
            if (top > dipHeight - 18)
                top = Math.Max(0, dipHeight - 36);
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE8, 0x10, 0x18, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0xA0, 0xF1)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 8, 4),
                MaxWidth = maxWidth,
                MaxHeight = 42,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = translation.Trim(),
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = Math.Max(56, maxWidth - 16),
                    MaxHeight = 34
                }
            };
            Canvas.SetLeft(card, Math.Max(0, box.X));
            Canvas.SetTop(card, Math.Max(0, top));
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
        var close = Chrome.Secondary("", SymbolRegular.Dismiss24, (_, _) => CloseOverlay());
        close.Padding = new Thickness(8, 8, 8, 8);
        close.ToolTip = L.Get("Action_Close");
        var hint = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE8, 0x10, 0x18, 0x28)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = L.Get("Screen_CloseHint"),
                Foreground = Brushes.White,
                FontSize = 13
            }
        };
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12) };
        bar.Children.Add(button);
        bar.Children.Add(close);
        bar.Children.Add(hint);
        var root = new Grid();
        root.Children.Add(FrozenImage(pick));
        root.Children.Add(canvas);
        root.Children.Add(bar);
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.Black,
            Topmost = true,
            ShowInTaskbar = false,
            Left = pick.DipX,
            Top = pick.DipY,
            Width = dipWidth,
            Height = dipHeight,
            Content = root,
            Focusable = true,
            ShowActivated = true
        };
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            window.Close();
        };
        window.SourceInitialized += (_, _) => ArmEscape(window);
        window.ContentRendered += (_, _) => FocusOverlay(window);
        window.Closed += (_, _) =>
        {
            ReleaseEscape(window);
            if (ReferenceEquals(_overlay, window))
                _overlay = null;
        };
        CloseOverlay();
        _overlay = window;
        window.Show();
        FocusOverlay(window);
    }

    private static void ArmEscape(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook((IntPtr _, int msg, IntPtr wParam, IntPtr __, ref bool handled) =>
        {
            var escape = (msg == WmKeyDown || msg == WmSysKeyDown) && wParam == (IntPtr)0x1B;
            var hotkey = msg == WmHotkey && wParam.ToInt32() == EscHotkeyId;
            if (!escape && !hotkey)
                return IntPtr.Zero;

            handled = true;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (window.IsLoaded)
                    window.Close();
            }));
            return IntPtr.Zero;
        });
        RegisterHotKey(hwnd, EscHotkeyId, 0x4000, 0x1B);
        FocusOverlay(window);
    }

    private static void ReleaseEscape(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
            UnregisterHotKey(hwnd, EscHotkeyId);
    }

    private static void FocusOverlay(Window window)
    {
        window.Activate();
        window.Focus();
        Keyboard.Focus(window);
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
            SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static (double X, double Y, double Width) TranslationBox(RegionPick pick, OcrLineBox line, double dipWidth, double dipHeight)
    {
        if (pick.PerMonitor && pick.Monitors.Count > 0)
        {
            var mapped = MonitorLayout.UnderLine(pick.ScreenX + line.X, pick.ScreenY + line.Y, line.Width, line.Height, pick.DipX, pick.DipY, pick.Monitors);
            if (mapped.Width > 0 || mapped.Height > 0)
                return (mapped.X, mapped.Y, Math.Max(72, mapped.Width));
        }

        return MonitorLayout.LinearUnder(line.X, line.Y, line.Width, line.Height, pick.Bitmap.PixelWidth, pick.Bitmap.PixelHeight, dipWidth, dipHeight);
    }

    private static UIElement FrozenImage(RegionPick pick)
    {
        if (!pick.PerMonitor || pick.Monitors.Count == 0)
            return new Image { Source = pick.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };

        var canvas = new Canvas { IsHitTestVisible = false };
        var cropRight = pick.ScreenX + pick.Bitmap.PixelWidth;
        var cropBottom = pick.ScreenY + pick.Bitmap.PixelHeight;
        foreach (var band in pick.Monitors)
        {
            var x1 = Math.Max(pick.ScreenX, band.X);
            var y1 = Math.Max(pick.ScreenY, band.Y);
            var x2 = Math.Min(cropRight, band.X + band.Width);
            var y2 = Math.Min(cropBottom, band.Y + band.Height);
            var width = x2 - x1;
            var height = y2 - y1;
            if (width <= 0 || height <= 0)
                continue;
            var crop = new CroppedBitmap(pick.Bitmap, new Int32Rect(x1 - pick.ScreenX, y1 - pick.ScreenY, width, height));
            crop.Freeze();
            var image = new Image
            {
                Source = crop,
                Stretch = Stretch.Fill,
                Width = width * 96d / band.DpiX,
                Height = height * 96d / band.DpiY,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(image, band.DipX + (x1 - band.X) * 96d / band.DpiX - pick.DipX);
            Canvas.SetTop(image, band.DipY + (y1 - band.Y) * 96d / band.DpiY - pick.DipY);
            canvas.Children.Add(image);
        }

        return canvas.Children.Count > 0
            ? canvas
            : new Image { Source = pick.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };
    }
}

public sealed class ScreenTranslateView : UserControl
{
    public ScreenTranslateView(IKutuHost host)
    {
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Screen_Desc")));
        var gesture = ToolHotkeyMap.Get(host.Settings, "screen-translate");
        if (!string.IsNullOrWhiteSpace(gesture))
            stack.Children.Add(Chrome.Hint(string.Format(L.Get("Dash_Hint"), gesture)));
        stack.Children.Add(Chrome.Hint(L.Get("Screen_Target") + ": " + host.Settings.ScreenTranslateTarget));
        stack.Children.Add(Chrome.Primary(L.Get("Action_ReadScreen"), SymbolRegular.Translate24, async (_, _) => await ScreenTranslateController.StartAsync(host)));
        Content = stack;
    }
}

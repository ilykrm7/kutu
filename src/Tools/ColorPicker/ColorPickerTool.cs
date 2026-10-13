using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Color;
using Kutu.Core.Models;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;

namespace Kutu.Tools.ColorPicker;

public sealed class ColorPickerTool : IKutuTool
{
    public string Id => "color-picker";

    public string TitleKey => "Tool_Color_Title";

    public string DescriptionKey => "Tool_Color_Desc";

    public string Symbol => "Color24";

    public IReadOnlyList<string> Aliases { get; } = ["renk", "color", "hex", "pipette", "damlalık", "damlalik", "picker"];

    public int Order => 40;

    public UIElement CreateView(IKutuHost host) => new ColorPickerView(host);
}

public sealed class ColorPickerView : UserControl
{
    private readonly IKutuHost _host;
    private readonly StackPanel _history;

    public ColorPickerView(IKutuHost host)
    {
        _host = host;
        _history = new StackPanel();
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Color_Hint")));
        stack.Children.Add(Chrome.Primary(L.Get("Action_PickColor"), SymbolRegular.Eyedropper24, (_, _) => ColorPickerController.Start(host, Refresh)));
        stack.Children.Add(Chrome.Heading(L.Get("Color_History")));
        stack.Children.Add(_history);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Refresh();
    }

    private void Refresh()
    {
        _history.Children.Clear();
        if (_host.Settings.ColorHistory.Count == 0)
        {
            _history.Children.Add(Chrome.Hint(L.Get("Color_Empty")));
            return;
        }

        var wrap = new WrapPanel();
        foreach (var swatch in _host.Settings.ColorHistory)
        {
            if (!RgbColor.TryParseHex(swatch.Hex, out var rgb))
                continue;

            var chip = new Border
            {
                Width = 92,
                Height = 64,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B)),
                Cursor = Cursors.Hand,
                ToolTip = swatch.Hex,
                Child = new TextBlock
                {
                    Text = swatch.Hex,
                    Foreground = rgb.R + rgb.G + rgb.B > 380 ? Brushes.Black : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                }
            };
            chip.MouseLeftButtonUp += (_, _) =>
            {
                _host.CopyText(swatch.Hex);
                _host.Toast(string.Format(L.Get("Color_Copied"), swatch.Hex));
            };
            wrap.Children.Add(chip);
        }

        _history.Children.Add(wrap);
    }
}

public static class ColorPickerController
{
    public static void Start(IKutuHost host, Action? onPicked = null)
    {
        var window = new ColorLensWindow(host, onPicked);
        window.Show();
    }
}

public sealed class ColorLensWindow : Window
{
    private readonly IKutuHost _host;
    private readonly Action? _onPicked;
    private readonly HookProc _mouseProc;
    private readonly HookProc _keyProc;
    private readonly TextBlock _hex;
    private readonly TextBlock _detail;
    private readonly Border _swatch;
    private readonly Image _grid;
    private IntPtr _mouseHook;
    private IntPtr _keyHook;
    private RgbColor _color;

    public ColorLensWindow(IKutuHost host, Action? onPicked)
    {
        _host = host;
        _onPicked = onPicked;
        _mouseProc = MouseHook;
        _keyProc = KeyHook;
        Width = 280;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        _hex = new TextBlock { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
        _detail = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(0xC5, 0xD0, 0xE0)), Margin = new Thickness(0, 4, 0, 0) };
        _swatch = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 12, 0) };
        _grid = new Image { Width = 132, Height = 132, SnapsToDevicePixels = true };
        RenderOptions.SetBitmapScalingMode(_grid, BitmapScalingMode.NearestNeighbor);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_hex);
        text.Children.Add(_detail);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_swatch);
        row.Children.Add(text);
        var root = new StackPanel();
        root.Children.Add(_grid);
        root.Children.Add(new Border { Height = 12 });
        root.Children.Add(row);
        root.Children.Add(Chrome.Hint(L.Get("Color_Hint")));
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF4, 0x10, 0x18, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0xA0, 0xF1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            Child = root
        };
        Loaded += (_, _) =>
        {
            _mouseHook = SetWindowsHookEx(14, _mouseProc, GetModuleHandle(null), 0);
            _keyHook = SetWindowsHookEx(13, _keyProc, GetModuleHandle(null), 0);
            Activate();
        };
        Closed += (_, _) =>
        {
            if (_mouseHook != IntPtr.Zero)
                UnhookWindowsHookEx(_mouseHook);
            if (_keyHook != IntPtr.Zero)
                UnhookWindowsHookEx(_keyHook);
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        SourceInitialized += (_, _) => Activate();
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            if (msg is 0x0200 or 0x0201)
            {
                GetCursorPos(out var point);
                Dispatcher.BeginInvoke(() => MoveTo(point.X, point.Y));
                if (msg == 0x0201)
                {
                    Dispatcher.BeginInvoke(Pick);
                    return (IntPtr)1;
                }
            }

            if (msg == 0x0204)
            {
                Dispatcher.BeginInvoke(Close);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == 0x0100)
        {
            var vk = Marshal.ReadInt32(lParam);
            if (vk == 0x1B)
            {
                Dispatcher.BeginInvoke(Close);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    private void MoveTo(int x, int y)
    {
        var color = Sample(x, y);
        _color = color;
        _hex.Text = color.Hex;
        _detail.Text = color.Rgb + "   " + color.Hsl;
        _swatch.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        _grid.Source = Magnifier(x, y);
        double left = x + 28;
        double top = y + 28;
        if (left + Width > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
            left = x - Width - 28;
        if (top + 260 > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
            top = y - 260;
        Left = left;
        Top = top;
    }

    private async void Pick()
    {
        var hex = _color.Hex;
        _host.CopyText(hex);
        _host.Settings.ColorHistory.RemoveAll(x => x.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase));
        _host.Settings.ColorHistory.Insert(0, new ColorSwatch { Hex = hex });
        if (_host.Settings.ColorHistory.Count > 24)
            _host.Settings.ColorHistory.RemoveRange(24, _host.Settings.ColorHistory.Count - 24);
        await _host.SaveSettingsAsync();
        _host.Toast(string.Format(L.Get("Color_Copied"), hex));
        _onPicked?.Invoke();
        Close();
    }

    private static RgbColor Sample(int x, int y)
    {
        var dc = GetDC(IntPtr.Zero);
        try
        {
            var pixel = GetPixel(dc, x, y);
            return new RgbColor((byte)(pixel & 0xFF), (byte)((pixel >> 8) & 0xFF), (byte)((pixel >> 16) & 0xFF));
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    private static BitmapSource Magnifier(int x, int y)
    {
        const int n = 11;
        var wb = new WriteableBitmap(n, n, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[n * n * 4];
        var dc = GetDC(IntPtr.Zero);
        try
        {
            for (var row = 0; row < n; row++)
            {
                for (var col = 0; col < n; col++)
                {
                    var pixel = GetPixel(dc, x + col - n / 2, y + row - n / 2);
                    var i = ((row * n) + col) * 4;
                    pixels[i] = (byte)(pixel & 0xFF);
                    pixels[i + 1] = (byte)((pixel >> 8) & 0xFF);
                    pixels[i + 2] = (byte)((pixel >> 16) & 0xFF);
                    pixels[i + 3] = 255;
                }
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }

        var mid = (n / 2) * n + (n / 2);
        var center = mid * 4;
        pixels[center] = 255;
        pixels[center + 1] = 255;
        pixels[center + 2] = 255;
        wb.WritePixels(new Int32Rect(0, 0, n, n), pixels, n * 4, 0);
        wb.Freeze();
        return wb;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

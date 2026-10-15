using Kutu.Core;
using Kutu.Core.Geometry;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kutu.Tools.Screenshot;

public sealed record RegionPick(
    BitmapSource Bitmap,
    int ScreenX,
    int ScreenY,
    int Width,
    int Height,
    double DipX,
    double DipY,
    double DipWidth,
    double DipHeight,
    bool PerMonitor,
    IReadOnlyList<MonitorLayout.Band> Monitors);

public sealed class RegionOverlay : Window
{
    private readonly CapturedScreen _screen;
    private readonly Rectangle _band;
    private readonly TextBlock _size;
    private Point _start;
    private bool _dragging;

    public RegionOverlay(CapturedScreen screen, bool offerFullScreen = false, string fullScreenLabel = "Tam ekran")
    {
        _screen = screen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Background = Brushes.Black;
        Cursor = Cursors.Cross;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        _band = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x4B, 0xDF, 0xB3)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 40, 160, 241)),
            Visibility = Visibility.Collapsed
        };
        _size = new TextBlock
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(180, 8, 12, 20)),
            Padding = new Thickness(6, 2, 6, 2),
            Visibility = Visibility.Collapsed
        };
        var canvas = new Canvas();
        canvas.Children.Add(new Rectangle
        {
            Width = Width,
            Height = Height,
            Fill = new SolidColorBrush(Color.FromArgb(48, 7, 11, 20)),
            IsHitTestVisible = false
        });
        canvas.Children.Add(_band);
        canvas.Children.Add(_size);
        var root = new Grid();
        root.Children.Add(CaptureImage(screen));
        root.Children.Add(canvas);
        if (offerFullScreen)
        {
            var full = new System.Windows.Controls.Button
            {
                Content = fullScreenLabel,
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 18, 0, 0)
            };
            full.Click += (_, _) => AcceptFull();
            root.Children.Add(full);
        }

        Content = root;
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            else if (offerFullScreen && e.Key == Key.Enter)
            {
                AcceptFull();
            }
        };
        Loaded += (_, _) => Activate();
    }

    public BitmapSource? Crop { get; private set; }

    public Int32Rect PixelRect { get; private set; }

    public Rect ScreenDip { get; private set; }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _start = e.GetPosition(this);
        CaptureMouse();
        _band.Visibility = Visibility.Visible;
        _size.Visibility = Visibility.Visible;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        var current = e.GetPosition(this);
        var rect = Rect(current);
        Canvas.SetLeft(_band, rect.X);
        Canvas.SetTop(_band, rect.Y);
        _band.Width = rect.Width;
        _band.Height = rect.Height;
        var pixels = ToPixels(rect);
        _size.Text = $"{pixels.Width} × {pixels.Height}";
        Canvas.SetLeft(_size, rect.X);
        Canvas.SetTop(_size, Math.Max(0, rect.Y - 26));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        ReleaseMouseCapture();
        var local = Rect(e.GetPosition(this));
        var rect = ToPixels(local);
        if (rect.Width < 4 || rect.Height < 4)
        {
            DialogResult = false;
            Close();
            return;
        }

        Accept(local, rect);
    }

    private void AcceptFull()
    {
        Accept(new Rect(0, 0, Width, Height), new Int32Rect(0, 0, _screen.Bitmap.PixelWidth, _screen.Bitmap.PixelHeight));
    }

    private void Accept(Rect localDip, Int32Rect pixels)
    {
        var cropped = pixels.Width == _screen.Bitmap.PixelWidth && pixels.Height == _screen.Bitmap.PixelHeight && pixels.X == 0 && pixels.Y == 0
            ? _screen.Bitmap
            : (BitmapSource)new CroppedBitmap(_screen.Bitmap, pixels);
        if (cropped.CanFreeze)
            cropped.Freeze();
        Crop = cropped;
        PixelRect = pixels;
        ScreenDip = new Rect(Left + localDip.X, Top + localDip.Y, Math.Max(1, localDip.Width), Math.Max(1, localDip.Height));
        DialogResult = true;
        Close();
    }

    public static RegionPick? Choose(bool offerFullScreen, string fullScreenLabel)
    {
        var screen = ScreenCapture.Capture();
        var overlay = new RegionOverlay(screen, offerFullScreen, fullScreenLabel);
        var accepted = overlay.ShowDialog() == true && overlay.Crop is not null;
        if (!accepted)
            return null;
        var rect = overlay.PixelRect;
        var dip = overlay.ScreenDip;
        return new RegionPick(
            overlay.Crop!,
            screen.OriginX + rect.X,
            screen.OriginY + rect.Y,
            rect.Width,
            rect.Height,
            dip.X,
            dip.Y,
            dip.Width,
            dip.Height,
            screen.PerMonitor,
            screen.Monitors);
    }

    private static UIElement CaptureImage(CapturedScreen screen)
    {
        if (!screen.PerMonitor || screen.Monitors.Count == 0)
            return new Image { Source = screen.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };

        var canvas = new Canvas { IsHitTestVisible = false };
        foreach (var band in screen.Monitors)
        {
            var localX = band.X - screen.OriginX;
            var localY = band.Y - screen.OriginY;
            var width = Math.Min(band.Width, screen.Bitmap.PixelWidth - localX);
            var height = Math.Min(band.Height, screen.Bitmap.PixelHeight - localY);
            if (localX < 0 || localY < 0 || width <= 0 || height <= 0)
                continue;
            var crop = new CroppedBitmap(screen.Bitmap, new Int32Rect(localX, localY, width, height));
            crop.Freeze();
            var image = new Image
            {
                Source = crop,
                Stretch = Stretch.Fill,
                Width = band.DipWidth,
                Height = band.DipHeight,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(image, band.DipX - SystemParameters.VirtualScreenLeft);
            Canvas.SetTop(image, band.DipY - SystemParameters.VirtualScreenTop);
            canvas.Children.Add(image);
        }

        return canvas.Children.Count > 0
            ? canvas
            : new Image { Source = screen.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };
    }

    private Rect Rect(Point current)
    {
        var x = Math.Min(_start.X, current.X);
        var y = Math.Min(_start.Y, current.Y);
        return new Rect(x, y, Math.Abs(current.X - _start.X), Math.Abs(current.Y - _start.Y));
    }

    private Int32Rect ToPixels(Rect dip)
    {
        int x, y, w, h;
        if (_screen.PerMonitor && _screen.Monitors.Count > 0)
        {
            var physical = MonitorLayout.PhysicalBounds(Left + dip.X, Top + dip.Y, dip.Width, dip.Height, _screen.Monitors);
            x = physical.X - _screen.OriginX;
            y = physical.Y - _screen.OriginY;
            w = physical.Width;
            h = physical.Height;
        }
        else
        {
            x = (int)Math.Round(dip.X * _screen.ScaleX);
            y = (int)Math.Round(dip.Y * _screen.ScaleY);
            w = (int)Math.Round(dip.Width * _screen.ScaleX);
            h = (int)Math.Round(dip.Height * _screen.ScaleY);
        }

        x = Num.Clamp(x, 0, Math.Max(0, _screen.Bitmap.PixelWidth - 1));
        y = Num.Clamp(y, 0, Math.Max(0, _screen.Bitmap.PixelHeight - 1));
        w = Num.Clamp(w, 1, _screen.Bitmap.PixelWidth - x);
        h = Num.Clamp(h, 1, _screen.Bitmap.PixelHeight - y);
        return new Int32Rect(x, y, w, h);
    }
}

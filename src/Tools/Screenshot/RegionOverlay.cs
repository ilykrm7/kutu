using Kutu.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kutu.Tools.Screenshot;

public sealed class RegionOverlay : Window
{
    private readonly CapturedScreen _screen;
    private readonly Rectangle _band;
    private readonly TextBlock _size;
    private Point _start;
    private bool _dragging;

    public RegionOverlay(CapturedScreen screen)
    {
        _screen = screen;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Background = new SolidColorBrush(Color.FromArgb(70, 7, 11, 20));
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
        canvas.Children.Add(_band);
        canvas.Children.Add(_size);
        Content = canvas;
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
        };
        Loaded += (_, _) => Activate();
    }

    public BitmapSource? Crop { get; private set; }

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
        var rect = ToPixels(Rect(e.GetPosition(this)));
        if (rect.Width < 4 || rect.Height < 4)
        {
            DialogResult = false;
            Close();
            return;
        }

        var cropped = new CroppedBitmap(_screen.Bitmap, rect);
        cropped.Freeze();
        Crop = cropped;
        DialogResult = true;
        Close();
    }

    private Rect Rect(Point current)
    {
        var x = Math.Min(_start.X, current.X);
        var y = Math.Min(_start.Y, current.Y);
        return new Rect(x, y, Math.Abs(current.X - _start.X), Math.Abs(current.Y - _start.Y));
    }

    private Int32Rect ToPixels(Rect dip)
    {
        var x = (int)Math.Round(dip.X * _screen.ScaleX);
        var y = (int)Math.Round(dip.Y * _screen.ScaleY);
        var w = (int)Math.Round(dip.Width * _screen.ScaleX);
        var h = (int)Math.Round(dip.Height * _screen.ScaleY);
        x = Num.Clamp(x, 0, _screen.Bitmap.PixelWidth - 1);
        y = Num.Clamp(y, 0, _screen.Bitmap.PixelHeight - 1);
        w = Num.Clamp(w, 1, _screen.Bitmap.PixelWidth - x);
        h = Num.Clamp(h, 1, _screen.Bitmap.PixelHeight - y);
        return new Int32Rect(x, y, w, h);
    }
}

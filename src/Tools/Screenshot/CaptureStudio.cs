using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using Rectangle = System.Windows.Shapes.Rectangle;
using TextBox = System.Windows.Controls.TextBox;

namespace Kutu.Tools.Screenshot;

public sealed class CaptureStudio : Window
{
    private readonly CapturedScreen _screen;
    private readonly IKutuHost _host;
    private readonly Canvas _overlay;
    private readonly Rectangle _dimTop;
    private readonly Rectangle _dimLeft;
    private readonly Rectangle _dimRight;
    private readonly Rectangle _dimBottom;
    private readonly Rectangle _frame;
    private readonly TextBlock _size;
    private readonly AnnLayer _layer;
    private readonly Border _toolbar;
    private readonly TextBox _text;
    private readonly List<Annotation> _items = [];
    private readonly List<Rectangle> _handles = [];
    private Rect _sel;
    private Point _anchor;
    private Point _dragOrigin;
    private Rect _dragRect;
    private bool _selecting;
    private bool _moving;
    private int _handle = -1;
    private bool _editing;
    private Annotation? _active;
    private ShotTool _tool = ShotTool.Arrow;
    private Color _color = Colors.White;
    private BitmapSource? _crop;

    public CaptureStudio(CapturedScreen screen, IKutuHost host)
    {
        _screen = screen;
        _host = host;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        Background = Brushes.Black;
        Cursor = Cursors.Cross;
        Focusable = true;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        var image = new Image
        {
            Source = screen.Bitmap,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        };
        _dimTop = Dim();
        _dimLeft = Dim();
        _dimRight = Dim();
        _dimBottom = Dim();
        _frame = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x4B, 0xDF, 0xB3)),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _size = new TextBlock
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(180, 8, 12, 20)),
            Padding = new Thickness(6, 2, 6, 2),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _layer = new AnnLayer { Visibility = Visibility.Collapsed };
        _text = new TextBox
        {
            Visibility = Visibility.Collapsed,
            MinWidth = 160,
            FontSize = 16,
            Padding = new Thickness(6, 4, 6, 4)
        };
        _text.KeyDown += OnTextKey;
        _toolbar = BuildToolbar();
        _overlay = new Canvas();
        foreach (var dim in new[] { _dimTop, _dimLeft, _dimRight, _dimBottom })
            _overlay.Children.Add(dim);
        _overlay.Children.Add(_layer);
        _overlay.Children.Add(_frame);
        _overlay.Children.Add(_size);
        _overlay.Children.Add(_text);
        _overlay.Children.Add(_toolbar);
        for (var i = 0; i < 8; i++)
        {
            var handle = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0x28, 0xA0, 0xF1)),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.SizeAll,
                Tag = i
            };
            handle.MouseLeftButtonDown += OnHandleDown;
            _handles.Add(handle);
            _overlay.Children.Add(handle);
        }

        Content = new Grid
        {
            Children = { image, _overlay }
        };
        _layer.MouseLeftButtonDown += OnDrawDown;
        _layer.MouseMove += OnDrawMove;
        _layer.MouseLeftButtonUp += OnDrawUp;
        MouseLeftButtonDown += OnSelectDown;
        MouseMove += OnSelectMove;
        MouseLeftButtonUp += OnSelectUp;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || _text.IsKeyboardFocused)
                return;
            Close();
            e.Handled = true;
        };
        Loaded += (_, _) =>
        {
            Activate();
            Focus();
        };
    }

    private Border BuildToolbar()
    {
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(ToolButton(L.Get("Shot_Move"), ShotTool.Arrow, move: true));
        tools.Children.Add(ToolButton(L.Get("Shot_Arrow"), ShotTool.Arrow, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Rect"), ShotTool.Rectangle, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Pen"), ShotTool.Pen, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Text"), ShotTool.Text, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Blur"), ShotTool.Blur, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Highlight"), ShotTool.Highlight, move: false));
        tools.Children.Add(Chrome.Secondary(L.Get("Shot_Undo"), SymbolRegular.ArrowUndo24, (_, _) =>
        {
            if (_items.Count > 0)
                _items.RemoveAt(_items.Count - 1);
            _layer.InvalidateVisual();
        }));
        tools.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) => Copy()));
        tools.Children.Add(Chrome.Secondary(L.Get("Action_Save"), SymbolRegular.Save24, (_, _) => Save()));
        tools.Children.Add(Chrome.Soon(L.Get("Action_Upload"), SymbolRegular.CloudArrowUp24));
        var bar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x10, 0x18, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x58)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8),
            Child = tools,
            Visibility = Visibility.Collapsed
        };
        return bar;
    }

    private Button ToolButton(string label, ShotTool tool, bool move)
    {
        var button = Chrome.Secondary(label, SymbolRegular.Circle24, (_, _) =>
        {
            _moving = move;
            if (!move)
                _tool = tool;
            Cursor = move ? Cursors.SizeAll : Cursors.Cross;
        });
        button.Margin = new Thickness(0, 0, 6, 6);
        return button;
    }

    private void OnSelectDown(object sender, MouseButtonEventArgs e)
    {
        if (_editing)
            return;
        _selecting = true;
        _anchor = e.GetPosition(this);
        CaptureMouse();
    }

    private void OnSelectMove(object sender, MouseEventArgs e)
    {
        if (!_selecting)
            return;
        _sel = Norm(_anchor, e.GetPosition(this));
        LayoutSelection(showChrome: false);
    }

    private void OnSelectUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
            return;
        _selecting = false;
        ReleaseMouseCapture();
        _sel = Norm(_anchor, e.GetPosition(this));
        if (_sel.Width < 8 || _sel.Height < 8)
        {
            HideSelection();
            return;
        }

        _editing = true;
        _moving = false;
        Cursor = Cursors.Arrow;
        LayoutSelection(showChrome: true);
        RefreshCrop();
    }

    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle handle)
            return;
        _handle = (int)handle.Tag;
        _dragOrigin = e.GetPosition(this);
        _dragRect = _sel;
        handle.CaptureMouse();
        e.Handled = true;
    }

    private void OnDrawDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editing)
            return;
        var dip = e.GetPosition(_layer);
        if (_moving)
        {
            _handle = 8;
            _dragOrigin = e.GetPosition(this);
            _dragRect = _sel;
            _layer.CaptureMouse();
            e.Handled = true;
            return;
        }

        var px = ToPixel(dip);
        _active = _tool switch
        {
            ShotTool.Arrow => new ArrowAnnotation { Start = px, End = px, Color = _color, Thickness = 4 },
            ShotTool.Rectangle => new RectAnnotation { Bounds = new Rect(px, new Size(1, 1)), Color = _color, Thickness = 4 },
            ShotTool.Highlight => new RectAnnotation { Bounds = new Rect(px, new Size(1, 1)), Color = Color.FromRgb(0xFF, 0xC8, 0x57), Filled = true, Thickness = 0 },
            ShotTool.Blur => new BlurAnnotation { Bounds = new Rect(px, new Size(1, 1)) },
            ShotTool.Pen => NewPen(px),
            ShotTool.Text => null,
            _ => null
        };
        if (_tool == ShotTool.Text)
        {
            Canvas.SetLeft(_text, _sel.X + dip.X);
            Canvas.SetTop(_text, _sel.Y + dip.Y);
            _text.Tag = px;
            _text.Text = "";
            _text.Visibility = Visibility.Visible;
            _text.Focus();
        }

        _layer.Active = _active;
        _layer.CaptureMouse();
        e.Handled = true;
    }

    private void OnDrawMove(object sender, MouseEventArgs e)
    {
        if (_handle == 8 && _layer.IsMouseCaptured)
        {
            MoveBy(e.GetPosition(this));
            return;
        }

        if (_active is null)
            return;
        var px = ToPixel(e.GetPosition(_layer));
        switch (_active)
        {
            case ArrowAnnotation arrow:
                arrow.End = px;
                break;
            case RectAnnotation rect:
                rect.Bounds = Norm(rect.Bounds.Location, px);
                break;
            case BlurAnnotation blur:
                blur.Bounds = Norm(blur.Bounds.Location, px);
                break;
            case PenAnnotation pen:
                pen.Points.Add(px);
                break;
        }

        _layer.InvalidateVisual();
    }

    private void OnDrawUp(object sender, MouseButtonEventArgs e)
    {
        if (_handle == 8)
        {
            _handle = -1;
            _layer.ReleaseMouseCapture();
            return;
        }

        if (_active is not null && _tool != ShotTool.Text)
            _items.Add(_active);
        _active = null;
        _layer.Active = null;
        _layer.ReleaseMouseCapture();
        _layer.InvalidateVisual();
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_handle is < 0 or 8)
            return;
        if (Mouse.Captured is not Rectangle)
            return;
        Resize(e.GetPosition(this));
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_handle is >= 0 and < 8)
        {
            Mouse.Capture(null);
            _handle = -1;
            RefreshCrop();
        }
    }

    private void OnTextKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _text.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || _text.Tag is not Point origin)
            return;
        var text = _text.Text.Trim();
        if (text.Length > 0)
        {
            _items.Add(new TextAnnotation { Origin = origin, Text = text, Color = _color, Thickness = 4 });
            _layer.InvalidateVisual();
        }

        _text.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void MoveBy(Point current)
    {
        var dx = current.X - _dragOrigin.X;
        var dy = current.Y - _dragOrigin.Y;
        var next = new Rect(_dragRect.X + dx, _dragRect.Y + dy, _dragRect.Width, _dragRect.Height);
        next.X = Math.Clamp(next.X, 0, Math.Max(0, Width - next.Width));
        next.Y = Math.Clamp(next.Y, 0, Math.Max(0, Height - next.Height));
        _sel = next;
        LayoutSelection(showChrome: true);
        RefreshCrop();
    }

    private void Resize(Point current)
    {
        var dx = current.X - _dragOrigin.X;
        var dy = current.Y - _dragOrigin.Y;
        var r = _dragRect;
        double x = r.X, y = r.Y, w = r.Width, h = r.Height;
        if (_handle is 0 or 3 or 5)
        {
            x += dx;
            w -= dx;
        }

        if (_handle is 2 or 4 or 7)
            w += dx;
        if (_handle is 0 or 1 or 2)
        {
            y += dy;
            h -= dy;
        }

        if (_handle is 5 or 6 or 7)
            h += dy;
        if (w < 12)
        {
            w = 12;
            x = r.X;
        }

        if (h < 12)
        {
            h = 12;
            y = r.Y;
        }

        _sel = new Rect(x, y, w, h);
        LayoutSelection(showChrome: true);
    }

    private void LayoutSelection(bool showChrome)
    {
        _frame.Visibility = Visibility.Visible;
        _size.Visibility = Visibility.Visible;
        Canvas.SetLeft(_frame, _sel.X);
        Canvas.SetTop(_frame, _sel.Y);
        _frame.Width = _sel.Width;
        _frame.Height = _sel.Height;
        var pixels = ToPixels(_sel);
        _size.Text = $"{pixels.Width} × {pixels.Height}";
        Canvas.SetLeft(_size, _sel.X);
        Canvas.SetTop(_size, Math.Max(0, _sel.Y - 26));
        Place(_dimTop, 0, 0, Width, _sel.Y);
        Place(_dimLeft, 0, _sel.Y, _sel.X, _sel.Height);
        Place(_dimRight, _sel.Right, _sel.Y, Math.Max(0, Width - _sel.Right), _sel.Height);
        Place(_dimBottom, 0, _sel.Bottom, Width, Math.Max(0, Height - _sel.Bottom));
        Canvas.SetLeft(_layer, _sel.X);
        Canvas.SetTop(_layer, _sel.Y);
        _layer.Width = _sel.Width;
        _layer.Height = _sel.Height;
        _layer.Visibility = showChrome ? Visibility.Visible : Visibility.Collapsed;
        _toolbar.Visibility = showChrome ? Visibility.Visible : Visibility.Collapsed;
        if (showChrome)
        {
            _toolbar.Measure(new Size(Width, Height));
            var bar = _toolbar.DesiredSize;
            var top = _sel.Bottom + 10;
            if (top + bar.Height > Height)
                top = Math.Max(8, _sel.Top - bar.Height - 10);
            var left = Math.Min(_sel.X, Math.Max(8, Width - bar.Width - 8));
            Canvas.SetLeft(_toolbar, left);
            Canvas.SetTop(_toolbar, top);
        }

        var spots = new[]
        {
            new Point(_sel.Left, _sel.Top),
            new Point(_sel.Left + _sel.Width / 2, _sel.Top),
            new Point(_sel.Right, _sel.Top),
            new Point(_sel.Left, _sel.Top + _sel.Height / 2),
            new Point(_sel.Right, _sel.Top + _sel.Height / 2),
            new Point(_sel.Left, _sel.Bottom),
            new Point(_sel.Left + _sel.Width / 2, _sel.Bottom),
            new Point(_sel.Right, _sel.Bottom)
        };
        for (var i = 0; i < _handles.Count; i++)
        {
            _handles[i].Visibility = showChrome ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(_handles[i], spots[i].X - 5);
            Canvas.SetTop(_handles[i], spots[i].Y - 5);
        }
    }

    private void HideSelection()
    {
        _frame.Visibility = Visibility.Collapsed;
        _size.Visibility = Visibility.Collapsed;
        _layer.Visibility = Visibility.Collapsed;
        _toolbar.Visibility = Visibility.Collapsed;
        foreach (var handle in _handles)
            handle.Visibility = Visibility.Collapsed;
        Place(_dimTop, 0, 0, 0, 0);
    }

    private void RefreshCrop()
    {
        var rect = ToPixels(_sel);
        if (rect.Width < 1 || rect.Height < 1)
            return;
        var crop = new CroppedBitmap(_screen.Bitmap, rect);
        crop.Freeze();
        _crop = crop;
        _layer.Source = crop;
        _layer.Items = _items;
        _layer.ScaleX = _screen.ScaleX;
        _layer.ScaleY = _screen.ScaleY;
        _layer.InvalidateVisual();
    }

    private void Copy()
    {
        var image = RenderResult();
        if (image is null)
            return;
        _host.CopyImage(image);
        _host.Toast(L.Get("Action_Copied"));
        Close();
    }

    private void Save()
    {
        var image = RenderResult();
        if (image is null)
            return;
        var dialog = new SaveFileDialog { Filter = "PNG|*.png", FileName = "kutu.png" };
        if (dialog.ShowDialog() != true)
            return;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = System.IO.File.Create(dialog.FileName);
        encoder.Save(stream);
        _host.Toast(L.Get("Shot_Saved"));
        Close();
    }

    private BitmapSource? RenderResult()
    {
        if (_crop is null)
            RefreshCrop();
        if (_crop is null)
            return null;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(_crop, new Rect(0, 0, _crop.PixelWidth, _crop.PixelHeight));
            foreach (var item in _items)
                item.Draw(dc, _crop);
        }

        var bitmap = new RenderTargetBitmap(_crop.PixelWidth, _crop.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private Point ToPixel(Point dip) => new(dip.X * _screen.ScaleX, dip.Y * _screen.ScaleY);

    private Int32Rect ToPixels(Rect dip)
    {
        var x = (int)Math.Round(dip.X * _screen.ScaleX);
        var y = (int)Math.Round(dip.Y * _screen.ScaleY);
        var w = (int)Math.Round(dip.Width * _screen.ScaleX);
        var h = (int)Math.Round(dip.Height * _screen.ScaleY);
        x = Math.Clamp(x, 0, Math.Max(0, _screen.Bitmap.PixelWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, _screen.Bitmap.PixelHeight - 1));
        w = Math.Clamp(w, 1, _screen.Bitmap.PixelWidth - x);
        h = Math.Clamp(h, 1, _screen.Bitmap.PixelHeight - y);
        return new Int32Rect(x, y, w, h);
    }

    private static Rect Norm(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rect(x, y, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    }

    private static PenAnnotation NewPen(Point start)
    {
        var pen = new PenAnnotation { Color = Colors.White, Thickness = 4 };
        pen.Points.Add(start);
        return pen;
    }

    private static Rectangle Dim() => new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(120, 7, 11, 20)),
        IsHitTestVisible = false
    };

    private static void Place(Rectangle rect, double x, double y, double w, double h)
    {
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        rect.Width = Math.Max(0, w);
        rect.Height = Math.Max(0, h);
    }

    private sealed class AnnLayer : FrameworkElement
    {
        public IReadOnlyList<Annotation> Items { get; set; } = [];

        public Annotation? Active { get; set; }

        public BitmapSource? Source { get; set; }

        public double ScaleX { get; set; } = 1;

        public double ScaleY { get; set; } = 1;

        protected override void OnRender(DrawingContext dc)
        {
            if (Source is null)
                return;
            dc.PushTransform(new ScaleTransform(1 / Math.Max(0.01, ScaleX), 1 / Math.Max(0.01, ScaleY)));
            foreach (var item in Items)
                item.Draw(dc, Source);
            Active?.Draw(dc, Source);
            dc.Pop();
        }
    }
}

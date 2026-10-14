using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Kutu.Core.Geometry;
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
    private readonly List<Button> _toolButtons = [];
    private readonly List<Button> _widthButtons = [];
    private Rect _sel;
    private Point _anchor;
    private Point _dragOrigin;
    private Rect _dragRect;
    private bool _selecting;
    private bool _moving;
    private int _handle = -1;
    private bool _editing;
    private Annotation? _active;
    private Point _drawOrigin;
    private ShotTool _tool = ShotTool.Arrow;
    private Color _color = Color.FromRgb(0xE5, 0x39, 0x35);
    private double _thickness = 4;
    private Border? _swatch;
    private Popup? _palette;
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
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBox)
            {
                Undo();
                e.Handled = true;
                return;
            }

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
        tools.Children.Add(ToolButton(L.Get("Shot_Move"), SymbolRegular.ArrowMove24, null, move: true));
        tools.Children.Add(ToolButton(L.Get("Shot_Pen"), SymbolRegular.InkStroke24, ShotTool.Pen, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Line"), SymbolRegular.Line24, ShotTool.Line, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Arrow"), SymbolRegular.ArrowRight24, ShotTool.Arrow, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Rect"), SymbolRegular.RectangleLandscape24, ShotTool.Rectangle, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Ellipse"), SymbolRegular.Oval24, ShotTool.Ellipse, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Text"), SymbolRegular.TextFont24, ShotTool.Text, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Marker"), SymbolRegular.Highlight24, ShotTool.Highlight, move: false));
        tools.Children.Add(ToolButton(L.Get("Shot_Blur"), SymbolRegular.Blur24, ShotTool.Blur, move: false));
        tools.Children.Add(ColorButton());
        tools.Children.Add(WidthButton(L.Get("Shot_Thin"), 2));
        tools.Children.Add(WidthButton(L.Get("Shot_Medium"), 4));
        tools.Children.Add(WidthButton(L.Get("Shot_Thick"), 8));
        tools.Children.Add(Chrome.Secondary(L.Get("Shot_Undo"), SymbolRegular.ArrowUndo24, (_, _) => Undo()));
        HighlightTools();
        HighlightWidth();
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

    private Button ToolButton(string label, SymbolRegular symbol, ShotTool? tool, bool move)
    {
        var button = Chrome.Secondary(label, symbol, (_, _) =>
        {
            _moving = move;
            if (tool is ShotTool chosen)
                _tool = chosen;
            Cursor = move ? Cursors.SizeAll : Cursors.Cross;
            HighlightTools();
        });
        button.Margin = new Thickness(0, 0, 6, 6);
        button.Tag = (tool, move);
        _toolButtons.Add(button);
        return button;
    }

    private UIElement ColorButton()
    {
        _swatch = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(_color),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 8, 6),
            Cursor = Cursors.Hand,
            ToolTip = L.Get("Shot_Color")
        };
        _swatch.MouseLeftButtonUp += (_, e) =>
        {
            TogglePalette();
            e.Handled = true;
        };
        return _swatch;
    }

    private Button WidthButton(string label, double thickness)
    {
        var button = Chrome.Secondary(label, SymbolRegular.LineThickness24, (_, _) =>
        {
            _thickness = thickness;
            HighlightWidth();
        });
        button.Margin = new Thickness(0, 0, 6, 6);
        button.Tag = thickness;
        _widthButtons.Add(button);
        return button;
    }

    private void HighlightTools()
    {
        foreach (var button in _toolButtons)
        {
            if (button.Tag is not ValueTuple<ShotTool?, bool> choice)
                continue;
            var on = _moving ? choice.Item2 : choice.Item1 == _tool && !choice.Item2;
            button.Appearance = on ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }
    }

    private void HighlightWidth()
    {
        foreach (var button in _widthButtons)
        {
            var on = button.Tag is double thickness && Math.Abs(thickness - _thickness) < 0.1;
            button.Appearance = on ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }
    }

    private void TogglePalette()
    {
        if (_swatch is null)
            return;
        if (_palette?.IsOpen == true)
        {
            _palette.IsOpen = false;
            return;
        }

        var colors = new[]
        {
            Color.FromRgb(0xE5, 0x39, 0x35),
            Color.FromRgb(0x2F, 0x80, 0xED),
            Color.FromRgb(0x27, 0xAE, 0x60),
            Color.FromRgb(0xF2, 0xC9, 0x4C),
            Color.FromRgb(0x11, 0x11, 0x11),
            Colors.White
        };
        var wrap = new WrapPanel { Width = 132 };
        foreach (var color in colors)
        {
            var chip = new Border
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            chip.MouseLeftButtonUp += (_, e) =>
            {
                ApplyColor(color);
                if (_palette is not null)
                    _palette.IsOpen = false;
                e.Handled = true;
            };
            wrap.Children.Add(chip);
        }

        var hex = new TextBox
        {
            Width = 88,
            Height = 28,
            Margin = new Thickness(4, 6, 4, 4),
            Text = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}",
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var apply = Chrome.Secondary(L.Get("Shot_Custom"), SymbolRegular.Color24, (_, _) =>
        {
            if (!TryParseHex(hex.Text, out var custom))
                return;
            ApplyColor(custom);
            if (_palette is not null)
                _palette.IsOpen = false;
        });
        apply.Margin = new Thickness(4, 4, 4, 4);
        var customRow = new StackPanel();
        customRow.Children.Add(hex);
        customRow.Children.Add(apply);
        var panel = new StackPanel();
        panel.Children.Add(wrap);
        panel.Children.Add(customRow);
        _palette = new Popup
        {
            PlacementTarget = _swatch,
            Placement = PlacementMode.Top,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x10, 0x18, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x58)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Child = panel
            }
        };
        _palette.IsOpen = true;
    }

    private void ApplyColor(Color color)
    {
        _color = color;
        if (_swatch is not null)
            _swatch.Background = new SolidColorBrush(color);
        _text.Foreground = new SolidColorBrush(color);
    }

    private static bool TryParseHex(string text, out Color color)
    {
        color = default;
        var hex = text.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];
        if (hex.Length != 6)
            return false;
        try
        {
            var rgb = Convert.ToInt32(hex, 16);
            color = Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void Undo()
    {
        if (_active is not null)
        {
            _active = null;
            _layer.Active = null;
            _layer.ReleaseMouseCapture();
            _layer.InvalidateVisual();
            return;
        }

        if (_items.Count == 0)
            return;
        _items.RemoveAt(_items.Count - 1);
        _layer.InvalidateVisual();
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

        var px = ClampPixel(ToPixel(dip));
        _drawOrigin = px;
        _active = _tool switch
        {
            ShotTool.Arrow => new ArrowAnnotation { Start = px, End = px, Color = _color, Thickness = _thickness },
            ShotTool.Line => new LineAnnotation { Start = px, End = px, Color = _color, Thickness = _thickness },
            ShotTool.Rectangle => new RectAnnotation { Bounds = new Rect(px, new Size(0, 0)), Color = _color, Thickness = _thickness },
            ShotTool.Ellipse => new EllipseAnnotation { Bounds = new Rect(px, new Size(0, 0)), Color = _color, Thickness = _thickness },
            ShotTool.Highlight => new RectAnnotation { Bounds = new Rect(px, new Size(0, 0)), Color = _color, Filled = true, Thickness = _thickness },
            ShotTool.Blur => new BlurAnnotation { Bounds = new Rect(px, new Size(0, 0)) },
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
            _text.Foreground = new SolidColorBrush(_color);
            _text.FontSize = _thickness <= 2.5 ? 16 : _thickness <= 5 ? 22 : 32;
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
        var px = ClampPixel(ToPixel(e.GetPosition(_layer)));
        var box = DragRect(_drawOrigin, px);
        switch (_active)
        {
            case ArrowAnnotation arrow:
                arrow.End = px;
                break;
            case LineAnnotation line:
                line.End = px;
                break;
            case RectAnnotation rect:
                rect.Bounds = box;
                break;
            case EllipseAnnotation ellipse:
                ellipse.Bounds = box;
                break;
            case BlurAnnotation blur:
                blur.Bounds = box;
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

        if (_active is not null && _tool != ShotTool.Text && HasExtent(_active))
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
            _items.Add(new TextAnnotation { Origin = origin, Text = text, Color = _color, Thickness = _thickness });
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

        try
        {
            _host.CopyImage(image);
            _host.Toast(L.Get("Action_Copied"));
        }
        catch (Exception ex)
        {
            _host.Log(ex.ToString());
            _host.Toast(L.Get("Shot_CopyFailed"));
        }

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

    private PenAnnotation NewPen(Point start)
    {
        var pen = new PenAnnotation { Color = _color, Thickness = _thickness };
        pen.Points.Add(start);
        return pen;
    }

    private Rect PixelBounds()
    {
        if (_crop is null)
            return new Rect(0, 0, 1, 1);
        return new Rect(0, 0, _crop.PixelWidth, _crop.PixelHeight);
    }

    private Point ClampPixel(Point point)
    {
        var bounds = PixelBounds();
        var clamped = ShotDrag.ClampPoint(point.X, point.Y, bounds.Width, bounds.Height);
        return new Point(clamped.X, clamped.Y);
    }

    private Rect DragRect(Point origin, Point current)
    {
        var bounds = PixelBounds();
        var drag = ShotDrag.FromPoints(origin.X, origin.Y, current.X, current.Y).Clamp(bounds.Width, bounds.Height);
        return new Rect(drag.X, drag.Y, drag.Width, drag.Height);
    }

    private static bool HasExtent(Annotation item) => item switch
    {
        RectAnnotation rect => rect.Bounds.Width >= 2 || rect.Bounds.Height >= 2,
        EllipseAnnotation ellipse => ellipse.Bounds.Width >= 2 || ellipse.Bounds.Height >= 2,
        BlurAnnotation blur => blur.Bounds.Width >= 4 && blur.Bounds.Height >= 4,
        ArrowAnnotation arrow => Span(arrow.Start, arrow.End) >= 2,
        LineAnnotation line => Span(line.Start, line.End) >= 2,
        PenAnnotation pen => pen.Points.Count >= 2,
        _ => true
    };

    private static double Span(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
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

        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
            new PointHitTestResult(this, hitTestParameters.HitPoint);

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

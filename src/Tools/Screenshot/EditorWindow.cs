using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MediaColor = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;

namespace Kutu.Tools.Screenshot;

public sealed class EditorWindow : Window
{
    private readonly IKutuHost _host;
    private readonly BitmapSource _source;
    private readonly List<Annotation> _items = [];
    private readonly ShotCanvas _canvas;
    private readonly TextBox _text;
    private ShotTool _tool = ShotTool.Arrow;
    private MediaColor _color = MediaColor.FromRgb(0xFF, 0x5A, 0x5A);
    private Annotation? _draft;
    private Point _origin;

    public EditorWindow(BitmapSource source, IKutuHost host)
    {
        _host = host;
        _source = source.Format == PixelFormats.Bgra32
            ? source
            : Freeze(new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0));
        Title = L.Get("Tool_Shot_Title");
        Width = 1100;
        Height = 760;
        MinWidth = 860;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(MediaColor.FromRgb(0x1C, 0x1C, 0x1E));
        _canvas = new ShotCanvas(_source, _items);
        _text = new TextBox
        {
            Width = 180,
            Height = 32,
            Text = L.Get("Shot_Text"),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(MediaColor.FromRgb(0x2C, 0x2C, 0x2E)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(MediaColor.FromRgb(0x38, 0x38, 0x3A))
        };

        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(ToolButton(L.Get("Shot_Arrow"), SymbolRegular.ArrowRight24, ShotTool.Arrow));
        tools.Children.Add(ToolButton(L.Get("Shot_Rect"), SymbolRegular.RectangleLandscape24, ShotTool.Rectangle));
        tools.Children.Add(ToolButton(L.Get("Shot_Pen"), SymbolRegular.InkStroke24, ShotTool.Pen));
        tools.Children.Add(ToolButton(L.Get("Shot_Text"), SymbolRegular.TextFont24, ShotTool.Text));
        tools.Children.Add(ToolButton(L.Get("Shot_Blur"), SymbolRegular.Blur24, ShotTool.Blur));
        tools.Children.Add(ToolButton(L.Get("Shot_Highlight"), SymbolRegular.Highlight24, ShotTool.Highlight));
        tools.Children.Add(_text);
        foreach (var color in new[] { "#FF5A5A", "#FFC857", "#4BDFB3", "#28A0F1", "#FFFFFF", "#111111" })
            tools.Children.Add(ColorDot(color));
        tools.Children.Add(Chrome.Secondary(L.Get("Shot_Undo"), SymbolRegular.ArrowUndo24, (_, _) =>
        {
            if (_items.Count > 0)
                _items.RemoveAt(_items.Count - 1);
            _canvas.InvalidateVisual();
        }));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) => Copy()));
        actions.Children.Add(Chrome.Secondary(L.Get("Action_Save"), SymbolRegular.Save24, (_, _) => Save()));
        actions.Children.Add(Chrome.Primary(L.Get("Action_Upload"), SymbolRegular.CloudArrowUp24, async (_, _) => await UploadAsync()));

        var bar = new DockPanel { Margin = new Thickness(16, 12, 16, 8) };
        DockPanel.SetDock(actions, Dock.Right);
        bar.Children.Add(actions);
        bar.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = tools });

        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _canvas,
            Background = Brushes.Transparent
        };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(viewer);
        Content = root;

        _canvas.MouseLeftButtonDown += OnDown;
        _canvas.MouseMove += OnMove;
        _canvas.MouseLeftButtonUp += OnUp;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_items.Count > 0)
                    _items.RemoveAt(_items.Count - 1);
                _canvas.InvalidateVisual();
            }
        };
    }

    private Wpf.Ui.Controls.Button ToolButton(string text, SymbolRegular symbol, ShotTool tool)
    {
        var button = Chrome.Secondary(text, symbol, (_, _) => _tool = tool);
        button.Margin = new Thickness(0, 0, 6, 0);
        return button;
    }

    private Border ColorDot(string hex)
    {
        var color = (MediaColor)ColorConverter.ConvertFromString(hex);
        var dot = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(color),
            Margin = new Thickness(4, 6, 4, 0),
            Cursor = Cursors.Hand,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1)
        };
        dot.MouseLeftButtonUp += (_, _) => _color = color;
        return dot;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _origin = e.GetPosition(_canvas);
        _canvas.CaptureMouse();
        _draft = _tool switch
        {
            ShotTool.Pen => new PenAnnotation { Color = _color, Thickness = 4 },
            ShotTool.Text => null,
            _ => null
        };
        if (_draft is PenAnnotation pen)
        {
            pen.Points.Add(_origin);
            _items.Add(pen);
        }

        if (_tool == ShotTool.Text)
        {
            _items.Add(new TextAnnotation { Origin = _origin, Text = _text.Text, Color = _color, Thickness = 4 });
            _canvas.InvalidateVisual();
        }
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var point = e.GetPosition(_canvas);
        switch (_tool)
        {
            case ShotTool.Pen when _draft is PenAnnotation pen:
                pen.Points.Add(point);
                break;
            case ShotTool.Arrow:
                ReplaceDraft(new ArrowAnnotation { Start = _origin, End = point, Color = _color, Thickness = 4 });
                break;
            case ShotTool.Rectangle:
                ReplaceDraft(new RectAnnotation { Bounds = MakeRect(_origin, point), Color = _color, Thickness = 4 });
                break;
            case ShotTool.Highlight:
                ReplaceDraft(new RectAnnotation { Bounds = MakeRect(_origin, point), Color = MediaColor.FromRgb(0xFF, 0xC8, 0x57), Filled = true });
                break;
            case ShotTool.Blur:
                ReplaceDraft(new BlurAnnotation { Bounds = MakeRect(_origin, point) });
                break;
        }

        _canvas.InvalidateVisual();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        _canvas.ReleaseMouseCapture();
        _draft = null;
    }

    private void ReplaceDraft(Annotation annotation)
    {
        if (_draft is not null)
            _items.Remove(_draft);
        _draft = annotation;
        _items.Add(annotation);
    }

    private static Rect MakeRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private BitmapSource RenderFinal()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(_source, new Rect(0, 0, _source.PixelWidth, _source.PixelHeight));
            foreach (var item in _items)
                item.Draw(dc, _source);
        }

        var bitmap = new RenderTargetBitmap(_source.PixelWidth, _source.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private void Copy()
    {
        try
        {
            _host.CopyImage(RenderFinal());
            _host.Toast(L.Get("Action_Copied"));
        }
        catch (Exception ex)
        {
            _host.Log(ex.ToString());
            _host.Toast(L.Get("Shot_CopyFailed"));
        }
    }

    private void Save()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg",
            FileName = "kutu-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")
        };
        if (dialog.ShowDialog() != true)
            return;

        var bitmap = RenderFinal();
        BitmapEncoder encoder = dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            ? new JpegBitmapEncoder()
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(dialog.FileName);
        encoder.Save(stream);
        _host.Toast(L.Get("Shot_Saved"));
    }

    private async Task UploadAsync()
    {
        try
        {
            var bitmap = RenderFinal();
            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
            var url = await _host.UploadAsync(stream.ToArray(), "kutu.png", "image/png");
            if (!string.IsNullOrWhiteSpace(url))
            {
                _host.CopyText(url);
                _host.Toast(L.Get("Shot_Uploaded"));
            }
        }
        catch (Exception ex)
        {
            _host.Toast(ex.Message);
        }
    }

    private static BitmapSource Freeze(BitmapSource source)
    {
        source.Freeze();
        return source;
    }
}

public sealed class ShotCanvas : FrameworkElement
{
    private readonly BitmapSource _source;
    private readonly IReadOnlyList<Annotation> _items;

    public ShotCanvas(BitmapSource source, IReadOnlyList<Annotation> items)
    {
        _source = source;
        _items = items;
        Width = source.PixelWidth;
        Height = source.PixelHeight;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawImage(_source, new Rect(0, 0, _source.PixelWidth, _source.PixelHeight));
        foreach (var item in _items)
            item.Draw(drawingContext, _source);
    }
}

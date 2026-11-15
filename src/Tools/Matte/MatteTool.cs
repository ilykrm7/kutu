using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Imaging;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using Slider = System.Windows.Controls.Slider;

namespace Kutu.Tools.Matte;

public sealed class MatteTool : IKutuTool
{
    public string Id => "matte";

    public string TitleKey => "Tool_Matte_Title";

    public string DescriptionKey => "Tool_Matte_Desc";

    public string Symbol => "Cut24";

    public IReadOnlyList<string> Aliases { get; } = ["matte", "arka plan", "arkaplan", "background", "şeffaf", "seffaf"];

    public int Order => 22;

    public UIElement CreateView(IKutuHost host) => new MatteView(host);
}

public sealed class MatteView : UserControl
{
    private readonly IKutuHost _host;
    private readonly Image _preview;
    private readonly Slider _tolerance;
    private readonly CheckBox _holes;
    private readonly TextBlock _status;
    private readonly TextBlock _label;
    private string? _path;
    private BitmapSource? _result;

    public MatteView(IKutuHost host)
    {
        _host = host;
        _tolerance = new Slider
        {
            Minimum = 0,
            Maximum = 80,
            Value = 24,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _tolerance.ValueChanged += (_, _) => Render();
        _holes = new CheckBox
        {
            Content = L.Get("Matte_Holes"),
            Foreground = Chrome.Text,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _holes.Checked += (_, _) => Render();
        _holes.Unchecked += (_, _) => Render();
        var drop = Chrome.DropZone(L.Get("Matte_Empty"), out _label);
        drop.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        drop.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                Load(files[0]);
        };
        _preview = new Image
        {
            Stretch = Stretch.Uniform,
            MaxHeight = 360,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var stage = new Border
        {
            Background = Checker(),
            CornerRadius = new CornerRadius(16),
            MinHeight = 180,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 12),
            Child = _preview
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Browse"), SymbolRegular.FolderOpen24, (_, _) =>
        {
            var dialog = new OpenFileDialog { Filter = "Image|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.tif;*.tiff" };
            if (dialog.ShowDialog() == true)
                Load(dialog.FileName);
        }));
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Save"), SymbolRegular.Save24, (_, _) => Save()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (_result is null)
                return;
            _host.CopyImage(_result);
            _host.Toast(L.Get("Action_Copied"));
        }));
        _status = Chrome.Hint("");
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Matte_Hint")));
        stack.Children.Add(drop);
        stack.Children.Add(Chrome.Label(L.Get("Matte_Tolerance")));
        stack.Children.Add(_tolerance);
        stack.Children.Add(_holes);
        stack.Children.Add(stage);
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        Content = stack;
    }

    private void Load(string path)
    {
        _path = path;
        _label.Text = Path.GetFileName(path);
        Render();
    }

    private void Render()
    {
        var path = _path;
        if (path is null || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            var source = LoadBitmap(path);
            var width = source.PixelWidth;
            var height = source.PixelHeight;
            var pixels = new byte[width * height * 4];
            source.CopyPixels(pixels, width * 4, 0);
            var cleared = Kutu.Core.Imaging.Matte.ClearBackground(pixels, width, height, (int)_tolerance.Value, _holes.IsChecked == true, feather: 1);
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, cleared, width * 4);
            bitmap.Freeze();
            _result = bitmap;
            _preview.Source = bitmap;
            _status.Text = width + " × " + height;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _result = null;
        }
    }

    private void Save()
    {
        if (_result is null)
        {
            _status.Text = L.Get("Matte_Empty");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = Path.GetFileNameWithoutExtension(_path ?? "kutu") + "-matte.png"
        };
        if (dialog.ShowDialog() != true)
            return;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(_result));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
        _host.Toast(L.Get("Matte_Saved"));
        _status.Text = dialog.FileName;
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        BitmapSource source = image;
        if (image.Format != PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = image;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            source = converted;
        }

        if (source.CanFreeze)
            source.Freeze();
        return source;
    }

    private static Brush Checker()
    {
        var dark = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E));
        var light = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3C));
        dark.Freeze();
        light.Freeze();
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(8, 8, 8, 8))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 16, 16),
            ViewportUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }
}

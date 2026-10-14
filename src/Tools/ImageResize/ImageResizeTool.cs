using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Kutu.Imaging;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.ImageResize;

public sealed class ImageResizeTool : IKutuTool
{
    public string Id => "image-resize";

    public string TitleKey => "Tool_Resize_Title";

    public string DescriptionKey => "Tool_Resize_Desc";

    public string Symbol => "ResizeImage24";

    public IReadOnlyList<string> Aliases { get; } = ["resize", "boyut", "küçült", "kucult", "compress", "sıkıştır", "sikistir", "batch"];

    public int Order => 20;

    public UIElement CreateView(IKutuHost host) => new ImageResizeView(host);
}

public sealed class ImageResizeView : UserControl
{
    private readonly IKutuHost _host;
    private readonly List<string> _files = [];
    private readonly TextBlock _count;
    private readonly TextBlock _status;
    private readonly ComboBox _mode;
    private readonly ComboBox _format;
    private readonly TextBox _value;
    private readonly Slider _quality;
    private int _busy;

    public ImageResizeView(IKutuHost host)
    {
        _host = host;
        var drop = Chrome.DropZone(L.Get("Drop_Hint"), out _);
        drop.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        drop.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
                AddFiles(files);
        };

        _mode = Combo(
        [
            L.Get("Resize_LongEdge"),
            L.Get("Resize_Width"),
            L.Get("Resize_Height"),
            L.Get("Resize_Percent")
        ]);
        _format = Combo(["JPG", "WebP", "PNG"]);
        _value = Chrome.Input("1920");
        _value.Text = "1920";
        _value.Width = 120;
        _quality = new Slider
        {
            Minimum = 40,
            Maximum = 100,
            Value = Math.Clamp(host.Settings.JpegQuality, 40, 100),
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center
        };
        var qualityValue = new TextBlock
        {
            Foreground = Chrome.Text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Width = 32,
            Text = ((int)_quality.Value).ToString()
        };
        _quality.ValueChanged += (_, _) => qualityValue.Text = ((int)_quality.Value).ToString();
        var qualityRow = new StackPanel { Orientation = Orientation.Horizontal };
        qualityRow.Children.Add(_quality);
        qualityRow.Children.Add(qualityValue);

        var options = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        options.Children.Add(Field(L.Get("Resize_Mode"), _mode));
        options.Children.Add(Field(L.Get("Resize_Value"), _value));
        options.Children.Add(Field(L.Get("Image_Format"), _format));
        options.Children.Add(Field(L.Get("Image_Quality"), qualityRow));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Browse"), SymbolRegular.FolderOpen24, (_, _) => Browse()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Clear"), SymbolRegular.Dismiss24, (_, _) =>
        {
            _files.Clear();
            Refresh();
        }));
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Apply"), SymbolRegular.ResizeImage24, async (_, _) => await RunAsync()));

        _count = Chrome.Hint(string.Format(L.Get("Common_Files"), 0));
        _status = Chrome.Hint("");
        var stack = new StackPanel();
        stack.Children.Add(drop);
        stack.Children.Add(_count);
        stack.Children.Add(options);
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        Content = stack;
    }

    private async Task RunAsync()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return;

        try
        {
            if (_files.Count == 0)
            {
                _status.Text = L.Get("Image_None");
                return;
            }

            if (!int.TryParse(_value.Text.Trim(), out var value) || value <= 0)
            {
                _status.Text = L.Get("Resize_Value");
                return;
            }

            var mode = _mode.SelectedIndex switch
            {
                1 => FitMode.Width,
                2 => FitMode.Height,
                3 => FitMode.Percent,
                _ => FitMode.LongEdge
            };
            var format = _format.SelectedItem?.ToString() switch
            {
                "WebP" => KutuImageFormat.Webp,
                "PNG" => KutuImageFormat.Png,
                _ => KutuImageFormat.Jpeg
            };
            var done = 0;
            var failed = new List<string>();
            var heif = false;
            foreach (var file in _files.ToList())
            {
                _status.Text = $"{L.Get("Resize_Working")} {done + 1}/{_files.Count}";
                try
                {
                    await ImagePipeline.ResizeAndSaveAsync(file, ImageFormats.OutputPath(file, format), mode, value, format, (int)_quality.Value);
                    done++;
                }
                catch (HeifCodecMissingException ex)
                {
                    heif = true;
                    failed.Add(Path.GetFileName(file));
                    _host.Log(file + Environment.NewLine + ex);
                }
                catch (Exception ex)
                {
                    failed.Add(Path.GetFileName(file));
                    _host.Log(file + Environment.NewLine + ex);
                }
            }

            if (failed.Count == 0)
                _status.Text = string.Format(L.Get("Resize_Done"), done);
            else
            {
                _status.Text = string.Format(L.Get("Image_FailedFile"), string.Join(", ", failed));
                if (heif)
                    _status.Text = L.Get("Image_HeifMissing") + " " + _status.Text;
            }

            _host.Toast(_status.Text);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private void Browse()
    {
        var files = ImageFilePicker.Pick(OwnerHandle());
        if (files is { Length: > 0 })
            AddFiles(files);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        ImageFileList.AddDistinct(_files, files);
        Refresh();
    }

    private IntPtr OwnerHandle()
    {
        var window = Window.GetWindow(this);
        return window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
    }

    private void Refresh() => _count.Text = string.Format(L.Get("Common_Files"), _files.Count);

    private static ComboBox Combo(IEnumerable<string> items)
    {
        var box = new ComboBox
        {
            MinWidth = 150,
            Height = 36,
            Background = Chrome.Field,
            Foreground = Chrome.Text,
            BorderBrush = Chrome.Line
        };
        foreach (var item in items)
            box.Items.Add(item);
        box.SelectedIndex = 0;
        return box;
    }

    private static StackPanel Field(string label, UIElement input)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 18, 10) };
        panel.Children.Add(Chrome.Label(label));
        panel.Children.Add(input);
        return panel;
    }
}

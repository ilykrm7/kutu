using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Kutu.Imaging;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;

namespace Kutu.Tools.ImageConverter;

public sealed class ImageConverterView : UserControl, IAcceptsParameter
{
    private readonly IKutuHost _host;
    private readonly List<string> _files = [];
    private readonly TextBlock _count;
    private readonly TextBlock _status;
    private readonly ComboBox _format;
    private readonly Slider _quality;
    private int _busy;

    public ImageConverterView(IKutuHost host)
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

        _format = DarkCombo(["PNG", "JPG", "WebP", "ICO", "PDF", "BMP"]);
        _quality = new Slider
        {
            Minimum = 40,
            Maximum = 100,
            Value = host.Settings.JpegQuality,
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
            TickFrequency = 5,
            IsSnapToTickEnabled = true
        };
        var qualityValue = new TextBlock
        {
            Foreground = Chrome.Text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Width = 32
        };
        _quality.ValueChanged += (_, _) => qualityValue.Text = ((int)_quality.Value).ToString();
        qualityValue.Text = ((int)_quality.Value).ToString();

        var qualityRow = new StackPanel { Orientation = Orientation.Horizontal };
        qualityRow.Children.Add(_quality);
        qualityRow.Children.Add(qualityValue);

        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        options.Children.Add(Field(L.Get("Image_Format"), _format));
        options.Children.Add(Field(L.Get("Image_Quality"), qualityRow));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Browse"), SymbolRegular.FolderOpen24, (_, _) => Browse()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Clear"), SymbolRegular.Dismiss24, (_, _) =>
        {
            _files.Clear();
            RefreshCount();
        }));
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Convert"), SymbolRegular.ImageEdit24, async (_, _) => await ConvertAsync()));

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

    public void SetParameter(object? parameter)
    {
        if (parameter is IEnumerable<string> files)
            AddFiles(files);
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
        RefreshCount();
    }

    private IntPtr OwnerHandle()
    {
        var window = Window.GetWindow(this);
        return window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
    }

    private void RefreshCount() => _count.Text = string.Format(L.Get("Common_Files"), _files.Count);

    private async Task ConvertAsync()
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

            var format = _format.SelectedItem?.ToString() switch
            {
                "JPG" => KutuImageFormat.Jpeg,
                "WebP" => KutuImageFormat.Webp,
                "ICO" => KutuImageFormat.Ico,
                "PDF" => KutuImageFormat.Pdf,
                "BMP" => KutuImageFormat.Bmp,
                _ => KutuImageFormat.Png
            };
            var quality = (int)_quality.Value;
            var done = 0;
            var failed = new List<string>();
            var heif = false;
            foreach (var file in _files.ToList())
            {
                _status.Text = $"{L.Get("Image_Working")} {done + 1}/{_files.Count}";
                try
                {
                    await ImagePipeline.ConvertAsync(file, ImageFormats.OutputPath(file, format), format, quality);
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
                _status.Text = string.Format(L.Get("Image_Done"), done);
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

    private static StackPanel Field(string label, UIElement input)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 22, 0) };
        panel.Children.Add(Chrome.Label(label));
        panel.Children.Add(input);
        return panel;
    }

    private static ComboBox DarkCombo(IEnumerable<string> items)
    {
        var box = new ComboBox
        {
            MinWidth = 140,
            Height = 36,
            Background = Chrome.Field,
            Foreground = Chrome.Text,
            BorderBrush = Chrome.Line,
            Padding = new Thickness(8, 4, 8, 4)
        };
        foreach (var item in items)
            box.Items.Add(item);
        box.SelectedIndex = 0;
        return box;
    }
}

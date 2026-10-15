using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Kutu.Tools.Screenshot;
using Microsoft.Win32;
using QRCoder;
using Wpf.Ui.Controls;
using ZXing;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Qr;

public static class QrLaunch
{
    public static bool ReadScreen { get; set; }
}

public sealed class QrTool : IKutuTool
{
    public string Id => "qr";

    public string TitleKey => "Tool_Qr_Title";

    public string DescriptionKey => "Tool_Qr_Desc";

    public string Symbol => "QrCode24";

    public IReadOnlyList<string> Aliases { get; } = ["qr", "karekod", "kare kod", "barcode"];

    public int Order => 36;

    public UIElement CreateView(IKutuHost host) => new QrView(host);
}

public sealed class QrView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _text;
    private readonly TextBox _read;
    private readonly Image _preview;
    private readonly TextBlock _status;
    private byte[]? _png;

    public QrView(IKutuHost host)
    {
        _host = host;
        _text = Chrome.Input(L.Get("Qr_Text"), multiline: true);
        _text.MinHeight = 80;
        _read = Chrome.Input(L.Get("Qr_Result"), multiline: true);
        _read.IsReadOnly = true;
        _read.MinHeight = 80;
        _preview = new Image { MaxWidth = 280, MaxHeight = 280, Margin = new Thickness(0, 8, 0, 8), Stretch = System.Windows.Media.Stretch.Uniform };
        _status = Chrome.Hint("");
        var make = new StackPanel { Orientation = Orientation.Horizontal };
        make.Children.Add(Chrome.Primary(L.Get("Action_Generate"), SymbolRegular.QrCode24, (_, _) => Generate()));
        make.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) => CopyImage()));
        make.Children.Add(Chrome.Secondary(L.Get("Action_Save"), SymbolRegular.Save24, (_, _) => Save()));
        var read = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        read.Children.Add(Chrome.Secondary(L.Get("Action_ReadScreen"), SymbolRegular.ScanDash24, async (_, _) => await ReadScreenAsync()));
        read.Children.Add(Chrome.Secondary(L.Get("Action_ReadFile"), SymbolRegular.FolderOpen24, (_, _) => ReadFile()));
        read.Children.Add(Chrome.Secondary(L.Get("Action_ReadClip"), SymbolRegular.ClipboardPaste24, (_, _) => ReadClipboard()));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Qr_Desc")));
        stack.Children.Add(_text);
        stack.Children.Add(make);
        stack.Children.Add(_preview);
        stack.Children.Add(read);
        stack.Children.Add(_read);
        stack.Children.Add(_status);
        Content = stack;
        if (QrLaunch.ReadScreen)
        {
            QrLaunch.ReadScreen = false;
            Dispatcher.BeginInvoke(new Action(() => _ = ReadScreenAsync()));
        }
    }

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(_text.Text))
        {
            _status.Text = L.Get("Qr_Empty");
            return;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(_text.Text, QRCodeGenerator.ECCLevel.Q);
        _png = new PngByteQRCode(data).GetGraphic(12);
        var image = BitmapFiles.FromPng(_png);
        _preview.Source = image;
        _status.Text = "";
    }

    private void CopyImage()
    {
        if (_png is null)
            return;
        _host.CopyImage(BitmapFiles.FromPng(_png));
        _host.Toast(L.Get("Action_Copied"));
    }

    private void Save()
    {
        if (_png is null)
            return;
        var dialog = new SaveFileDialog { Filter = "PNG|*.png", FileName = "kutu-qr.png" };
        if (dialog.ShowDialog() != true)
            return;
        File.WriteAllBytes(dialog.FileName, _png);
        _status.Text = L.Get("Qr_Saved");
    }

    private async Task ReadScreenAsync()
    {
        _host.HideForCapture();
        try
        {
            await Task.Delay(160);
            var pick = RegionPicker();
            if (pick is not null)
                Decode(pick);
        }
        finally
        {
            _host.RestoreAfterCapture();
        }
    }

    private void ReadFile()
    {
        var dialog = new OpenFileDialog { Filter = "Image|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
        if (dialog.ShowDialog() != true)
            return;
        Decode(BitmapFiles.FromFile(dialog.FileName));
    }

    private void ReadClipboard()
    {
        var image = BitmapFiles.FromClipboard();
        if (image is null)
        {
            _status.Text = L.Get("Qr_None");
            return;
        }

        Decode(image);
    }

    private static BitmapSource? RegionPicker() => Kutu.Tools.Screenshot.RegionOverlay.Choose(false, "")?.Bitmap;

    private void Decode(BitmapSource source)
    {
        var (pixels, width, height) = BitmapFiles.Bgra32(source);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                TryHarder = true
            }
        };
        var luminance = new RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var result = reader.Decode(luminance);
        if (result is null || string.IsNullOrWhiteSpace(result.Text))
        {
            _status.Text = L.Get("Qr_None");
            return;
        }

        _read.Text = result.Text;
        _status.Text = "";
    }
}

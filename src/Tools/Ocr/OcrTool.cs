using System.Windows;
using System.Windows.Controls;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Kutu.Tools.Screenshot;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Ocr;

public static class OcrLaunch
{
    public static bool ReadScreen { get; set; }
}

public sealed class OcrTool : IKutuTool
{
    public string Id => "ocr";

    public string TitleKey => "Tool_Ocr_Title";

    public string DescriptionKey => "Tool_Ocr_Desc";

    public string Symbol => "ScanText24";

    public IReadOnlyList<string> Aliases { get; } = ["ocr", "metin oku", "tara", "read"];

    public int Order => 104;

    public UIElement CreateView(IKutuHost host) => new OcrView(host);
}

public sealed class OcrView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _output;
    private readonly TextBlock _status;

    public OcrView(IKutuHost host)
    {
        _host = host;
        _output = Chrome.Input(L.Get("Ocr_Empty"), multiline: true);
        _output.IsReadOnly = true;
        _status = Chrome.Hint(L.Get("Ocr_Hint"));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Chrome.Primary(L.Get("Action_ReadScreen"), SymbolRegular.ScanDash24, async (_, _) => await FromScreenAsync()));
        row.Children.Add(Chrome.Secondary(L.Get("Action_ReadFile"), SymbolRegular.FolderOpen24, async (_, _) => await FromFileAsync()));
        row.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_output.Text))
            {
                host.CopyText(_output.Text);
                host.Toast(L.Get("Action_Copied"));
            }
        }));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Ocr_Desc")));
        stack.Children.Add(row);
        stack.Children.Add(_output);
        stack.Children.Add(_status);
        Content = stack;
        if (OcrLaunch.ReadScreen)
        {
            OcrLaunch.ReadScreen = false;
            Dispatcher.BeginInvoke(new Action(() => _ = FromScreenAsync()));
        }
    }

    private async Task FromScreenAsync()
    {
        _host.HideForCapture();
        RegionPick? pick = null;
        try
        {
            await Task.Delay(160);
            pick = RegionOverlay.Choose(true, L.Get("Action_FullScreen"));
        }
        finally
        {
            _host.RestoreAfterCapture();
        }

        if (pick is not null)
            await ReadAsync(pick.Bitmap);
    }

    private async Task FromFileAsync()
    {
        var dialog = new OpenFileDialog { Filter = "Image|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff" };
        if (dialog.ShowDialog() != true)
            return;
        await ReadAsync(BitmapFiles.FromFile(dialog.FileName));
    }

    private async Task ReadAsync(System.Windows.Media.Imaging.BitmapSource source)
    {
        _status.Text = L.Get("Screen_Working");
        try
        {
            var page = await WindowsOcr.ReadAsync(source);
            _output.Text = page.Text;
            _status.Text = string.IsNullOrWhiteSpace(page.Text) ? L.Get("Ocr_Empty") : "";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message == "ocr-missing" ? L.Get("Ocr_Missing") : ex.Message;
        }
    }
}

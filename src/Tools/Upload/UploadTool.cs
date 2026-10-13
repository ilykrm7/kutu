using System.Text;
using System.Windows;
using System.Windows.Controls;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Upload;

public sealed class UploadTool : IKutuTool
{
    public string Id => "upload";

    public string TitleKey => "Tool_Upload_Title";

    public string DescriptionKey => "Tool_Upload_Desc";

    public string Symbol => "CloudArrowUp24";

    public IReadOnlyList<string> Aliases { get; } = ["upload", "yükle", "yukle", "link", "paylaş", "paylas", "kerim"];

    public int Order => 90;

    public UIElement CreateView(IKutuHost host) => new UploadView(host);
}

public sealed class UploadView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _text;
    private readonly TextBox _url;
    private readonly TextBlock _status;
    private string? _filePath;

    public UploadView(IKutuHost host)
    {
        _host = host;
        _text = Chrome.Input(L.Get("Upload_Placeholder"), multiline: true);
        var drop = Chrome.DropZone(L.Get("Upload_Drop"), out var dropLabel);
        drop.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        drop.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            {
                _filePath = files[0];
                dropLabel.Text = Path.GetFileName(_filePath);
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Browse"), SymbolRegular.FolderOpen24, (_, _) =>
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                _filePath = dialog.FileName;
                dropLabel.Text = Path.GetFileName(_filePath);
            }
        }));
        buttons.Children.Add(Chrome.Soon(L.Get("Action_Upload"), SymbolRegular.CloudArrowUp24));
        buttons.Children.Add(Chrome.Hint(L.Get("Dash_Soon")));
        _url = Chrome.Input(L.Get("Upload_Result"));
        _url.IsReadOnly = true;
        _status = Chrome.Hint("");
        var copy = Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_url.Text))
            {
                _host.CopyText(_url.Text);
                _host.Toast(L.Get("Action_Copied"));
            }
        });

        var stack = new StackPanel();
        stack.Children.Add(Chrome.Heading(L.Get("Upload_Text")));
        stack.Children.Add(_text);
        stack.Children.Add(new Border { Height = 12 });
        stack.Children.Add(Chrome.Heading(L.Get("Upload_File")));
        stack.Children.Add(drop);
        stack.Children.Add(buttons);
        stack.Children.Add(Chrome.Label(L.Get("Upload_Result")));
        stack.Children.Add(_url);
        stack.Children.Add(new Border { Height = 8 });
        stack.Children.Add(copy);
        stack.Children.Add(_status);
        Content = stack;
    }

    private async Task UploadAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_host.Settings.UploadEndpoint) || string.IsNullOrWhiteSpace(_host.Settings.UploadApiKey))
            {
                _status.Text = L.Get("Upload_NeedConfig");
                return;
            }

            byte[] data;
            string name;
            string type;
            if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath) && string.IsNullOrWhiteSpace(_text.Text))
            {
                data = await File.ReadAllBytesAsync(_filePath);
                name = Path.GetFileName(_filePath);
                type = "application/octet-stream";
            }
            else if (!string.IsNullOrWhiteSpace(_text.Text))
            {
                data = Encoding.UTF8.GetBytes(_text.Text);
                name = "note.txt";
                type = "text/plain";
            }
            else if (!string.IsNullOrWhiteSpace(_filePath))
            {
                data = await File.ReadAllBytesAsync(_filePath);
                name = Path.GetFileName(_filePath);
                type = "application/octet-stream";
            }
            else
            {
                _status.Text = L.Get("Translate_Empty");
                return;
            }

            _status.Text = L.Get("Common_Working");
            var url = await UploadClient.UploadAsync(_host.Settings.UploadEndpoint, _host.Settings.UploadApiKey, data, name, type);
            _url.Text = url;
            _host.CopyText(url);
            _status.Text = L.Get("Upload_Ok");
            _host.Toast(L.Get("Upload_Ok"));
        }
        catch (InvalidOperationException ex) when (ex.Message == "missing-config")
        {
            _status.Text = L.Get("Upload_NeedConfig");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }
}

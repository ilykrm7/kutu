using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Security;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Digest;

public sealed class DigestTool : IKutuTool
{
    public string Id => "digest";

    public string TitleKey => "Tool_Digest_Title";

    public string DescriptionKey => "Tool_Digest_Desc";

    public string Symbol => "Fingerprint24";

    public IReadOnlyList<string> Aliases { get; } = ["özet", "ozet", "hash", "sha", "md5", "sha256", "checksum"];

    public int Order => 116;

    public UIElement CreateView(IKutuHost host) => new DigestView(host);
}

public sealed class DigestView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _text;
    private readonly TextBlock _file;
    private readonly StackPanel _rows;
    private readonly TextBlock _status;
    private string? _filePath;

    public DigestView(IKutuHost host)
    {
        _host = host;
        _text = Chrome.Input(L.Get("Digest_Placeholder"), multiline: true);
        _file = new TextBlock { Foreground = Chrome.Muted, FontSize = 13, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        _rows = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        _status = Chrome.Hint("");
        var drop = Chrome.DropZone(L.Get("Digest_Drop"), out var dropLabel);
        drop.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        drop.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
                TakeFile(files[0], dropLabel);
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Browse"), SymbolRegular.FolderOpen24, (_, _) =>
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
                TakeFile(dialog.FileName, dropLabel);
        }));
        buttons.Children.Add(Chrome.Primary(L.Get("Digest_Run"), SymbolRegular.Fingerprint24, (_, _) => Compute(dropLabel)));
        buttons.Children.Add(Chrome.Secondary(L.Get("Digest_Clear"), SymbolRegular.Dismiss24, (_, _) =>
        {
            _filePath = null;
            _file.Text = "";
            dropLabel.Text = L.Get("Digest_Drop");
            _rows.Children.Clear();
            _status.Text = "";
        }));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Digest_Desc")));
        stack.Children.Add(Chrome.Label(L.Get("Digest_Text")));
        stack.Children.Add(_text);
        stack.Children.Add(new Border { Height = 12 });
        stack.Children.Add(Chrome.Label(L.Get("Digest_File")));
        stack.Children.Add(drop);
        stack.Children.Add(buttons);
        stack.Children.Add(_file);
        stack.Children.Add(_rows);
        stack.Children.Add(_status);
        Content = stack;
    }

    private void TakeFile(string path, TextBlock dropLabel)
    {
        _filePath = path;
        dropLabel.Text = Path.GetFileName(path);
        _file.Text = path;
    }

    private void Compute(TextBlock dropLabel)
    {
        try
        {
            byte[] data;
            if (!string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath))
            {
                data = File.ReadAllBytes(_filePath);
                _status.Text = Path.GetFileName(_filePath);
            }
            else if (!string.IsNullOrEmpty(_text.Text))
            {
                data = Digests.Utf8(_text.Text);
                _status.Text = L.Get("Digest_Text");
            }
            else
            {
                _status.Text = L.Get("Digest_Empty");
                _rows.Children.Clear();
                return;
            }

            _rows.Children.Clear();
            foreach (var name in Digests.Algorithms)
            {
                var value = Digests.Compute(name, data);
                var captured = value;
                _rows.Children.Add(Chrome.ValueRow(name, value, () =>
                {
                    _host.CopyText(captured);
                    _host.Toast(L.Get("Action_Copied"));
                }));
            }

            dropLabel.Text = string.IsNullOrWhiteSpace(_filePath) ? L.Get("Digest_Drop") : Path.GetFileName(_filePath);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }
}

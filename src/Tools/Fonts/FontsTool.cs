using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Fonts;

public sealed class FontsTool : IKutuTool
{
    public string Id => "fonts";

    public string TitleKey => "Tool_Font_Title";

    public string DescriptionKey => "Tool_Font_Desc";

    public string Symbol => "TextFont24";

    public IReadOnlyList<string> Aliases { get; } = ["font", "yazı", "yazi", "yazı tipi", "typeface"];

    public int Order => 42;

    public UIElement CreateView(IKutuHost host) => new FontsView(host);
}

public sealed class FontsView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _query;
    private readonly TextBox _sample;
    private readonly TextBox _size;
    private readonly ListBox _list;
    private readonly TextBlock _preview;
    private readonly List<string> _families;

    public FontsView(IKutuHost host)
    {
        _host = host;
        _families = System.Windows.Media.Fonts.SystemFontFamilies
            .Select(family => family.Source)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _query = Chrome.Input(L.Get("Font_Search"));
        _query.TextChanged += (_, _) => Refill();
        _sample = Chrome.Input(L.Get("Font_Sample"));
        _sample.Text = L.Get("Font_SampleDefault");
        _sample.TextChanged += (_, _) => ApplyPreview();
        _size = Chrome.Input("32");
        _size.Text = "32";
        _size.Width = 72;
        _size.TextChanged += (_, _) => ApplyPreview();
        _list = new ListBox
        {
            Background = Chrome.Field,
            Foreground = Chrome.Text,
            BorderThickness = new Thickness(0),
            MinHeight = 280,
            MaxHeight = 360
        };
        _list.ItemContainerStyle = new Style(typeof(ListBoxItem))
        {
            Setters =
            {
                new Setter(Control.ForegroundProperty, Chrome.Text),
                new Setter(Control.PaddingProperty, new Thickness(10, 7, 10, 7)),
                new Setter(Control.FontSizeProperty, 14.0),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)
            }
        };
        _list.SelectionChanged += (_, _) => ApplyPreview();
        _preview = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Chrome.Text,
            FontSize = 32,
            Margin = new Thickness(4, 8, 4, 8)
        };
        var copy = Chrome.Secondary(L.Get("Font_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (_list.SelectedItem is string name && name.Length > 0)
            {
                _host.CopyText(name);
                _host.Toast(L.Get("Action_Copied"));
            }
        });
        var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        sizeRow.Children.Add(_size);
        sizeRow.Children.Add(copy);
        var previewCard = Chrome.Card(_preview);
        previewCard.MinHeight = 96;
        previewCard.Margin = new Thickness(0, 0, 0, 12);
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Font_Desc")));
        stack.Children.Add(_query);
        stack.Children.Add(Chrome.Label(L.Get("Font_Sample")));
        stack.Children.Add(_sample);
        stack.Children.Add(Chrome.Label(L.Get("Font_Size")));
        stack.Children.Add(sizeRow);
        stack.Children.Add(previewCard);
        stack.Children.Add(_list);
        Content = stack;
        Refill();
    }

    private void Refill()
    {
        var query = _query.Text.Trim();
        _list.Items.Clear();
        foreach (var name in _families)
        {
            if (query.Length == 0 || name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                _list.Items.Add(name);
        }

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
        else
            ApplyPreview();
    }

    private void ApplyPreview()
    {
        var sample = string.IsNullOrWhiteSpace(_sample.Text) ? L.Get("Font_SampleDefault") : _sample.Text;
        _preview.Text = sample;
        var size = 32d;
        if (double.TryParse(_size.Text.Trim(), out var parsed))
            size = parsed;
        if (size < 10)
            size = 10;
        if (size > 120)
            size = 120;
        _preview.FontSize = size;
        if (_list.SelectedItem is string name)
        {
            _preview.FontFamily = new FontFamily(name);
            _preview.Opacity = 1;
        }
        else
        {
            _preview.Text = L.Get("Font_Empty");
            _preview.Opacity = 0.7;
        }
    }
}

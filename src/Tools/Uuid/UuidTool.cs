using System.Text;
using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Security;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Uuid;

public sealed class UuidTool : IKutuTool
{
    public string Id => "uuid";

    public string TitleKey => "Tool_Uuid_Title";

    public string DescriptionKey => "Tool_Uuid_Desc";

    public string Symbol => "NumberSymbol24";

    public IReadOnlyList<string> Aliases { get; } = ["uuid", "guid", "kimlik", "id"];

    public int Order => 114;

    public UIElement CreateView(IKutuHost host) => new UuidView(host);
}

public sealed class UuidView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _count;
    private readonly TextBox _output;
    private readonly CheckBox _upper;
    private readonly CheckBox _braces;
    private readonly CheckBox _hyphens;
    private readonly CheckBox _version7;

    public UuidView(IKutuHost host)
    {
        _host = host;
        _count = Chrome.Input("1");
        _count.Text = "1";
        _count.Width = 88;
        _upper = Box(L.Get("Uuid_Upper"));
        _braces = Box(L.Get("Uuid_Braces"));
        _hyphens = Box(L.Get("Uuid_Hyphens"));
        _hyphens.IsChecked = true;
        _version7 = Box(L.Get("Uuid_V7"));
        _output = Chrome.Input("UUID", multiline: true);
        _output.IsReadOnly = true;
        _output.MinHeight = 88;
        var checks = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        checks.Children.Add(_version7);
        checks.Children.Add(_upper);
        checks.Children.Add(_braces);
        checks.Children.Add(_hyphens);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Chrome.Primary(L.Get("Uuid_Generate"), SymbolRegular.NumberSymbol24, (_, _) => Generate()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_output.Text))
            {
                _host.CopyText(_output.Text.Trim());
                _host.Toast(L.Get("Action_Copied"));
            }
        }));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_Uuid_Desc")));
        stack.Children.Add(Chrome.Label(L.Get("Uuid_Count")));
        stack.Children.Add(_count);
        stack.Children.Add(checks);
        stack.Children.Add(buttons);
        stack.Children.Add(new Border { Height = 12 });
        stack.Children.Add(_output);
        Content = stack;
        Generate();
    }

    private void Generate()
    {
        var count = 1;
        if (int.TryParse(_count.Text.Trim(), out var parsed))
            count = parsed;
        if (count < 1)
            count = 1;
        if (count > 100)
            count = 100;
        _count.Text = count.ToString();
        var version7 = _version7.IsChecked == true;
        var upper = _upper.IsChecked == true;
        var braces = _braces.IsChecked == true;
        var hyphens = _hyphens.IsChecked == true;
        var builder = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            var id = version7 ? UuidFormat.CreateVersion7() : Guid.NewGuid();
            if (i > 0)
                builder.AppendLine();
            builder.Append(UuidFormat.Format(id, upper, braces, hyphens));
        }

        _output.Text = builder.ToString();
    }

    private static CheckBox Box(string label) => new()
    {
        Content = label,
        Foreground = Chrome.Text,
        Margin = new Thickness(0, 0, 16, 8)
    };
}

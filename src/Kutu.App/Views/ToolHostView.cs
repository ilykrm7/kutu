using System.Windows;
using System.Windows.Controls;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;

namespace Kutu.App.Views;

public sealed class ToolHostView : UserControl
{
    public ToolHostView(IKutuTool tool, UIElement body, Action back)
    {
        var icon = new SymbolIcon
        {
            Symbol = Chrome.Symbol(tool.Symbol),
            FontSize = 22,
            Foreground = System.Windows.Media.Brushes.White
        };
        var bubble = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = Chrome.Accent,
            Child = icon,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var title = new TextBlock
        {
            Text = L.Get(tool.TitleKey),
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Foreground = Chrome.Text
        };
        var desc = new TextBlock
        {
            Text = L.Get(tool.DescriptionKey),
            Foreground = Chrome.Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };
        var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(title);
        text.Children.Add(desc);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 16) };
        header.Children.Add(Chrome.Secondary(L.Get("Action_Back"), SymbolRegular.ArrowLeft24, (_, _) => back()));
        header.Children.Add(bubble);
        header.Children.Add(text);

        var page = new StackPanel();
        page.Children.Add(header);
        page.Children.Add(body);
        Content = Chrome.ScrollPage(page, 880);
    }
}

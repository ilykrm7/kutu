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
            Symbol = Enum.TryParse<SymbolRegular>(tool.Symbol, out var symbol) ? symbol : SymbolRegular.Circle24,
            FontSize = 22,
            Foreground = System.Windows.Media.Brushes.White
        };
        var bubble = new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(14),
            Background = Chrome.Accent,
            Child = icon,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var title = new TextBlock
        {
            Text = L.Get(tool.TitleKey),
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            Foreground = Chrome.Text
        };
        var desc = new TextBlock
        {
            Text = L.Get(tool.DescriptionKey),
            Foreground = Chrome.Muted,
            Margin = new Thickness(0, 2, 0, 0)
        };
        var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(title);
        text.Children.Add(desc);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(Chrome.Secondary(L.Get("Action_Back"), SymbolRegular.ArrowLeft24, (_, _) => back()));
        header.Children.Add(bubble);
        header.Children.Add(text);

        var root = new DockPanel { Margin = new Thickness(28, 12, 28, 20) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(body);
        Content = root;
    }
}

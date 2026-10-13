using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Wpf.Ui.Controls;

namespace Kutu.App.Views;

public partial class DashboardView : UserControl
{
    private readonly IKutuHost _host;

    public DashboardView(IKutuHost host)
    {
        _host = host;
        InitializeComponent();
        Greeting.Text = L.Get("Dash_Greeting");
        Tagline.Text = L.Get("App_Tagline");
        SearchBox.PlaceholderText = L.Get("Dash_SearchPlaceholder");
        SearchBox.TextChanged += (_, _) => Refresh();
    }

    public void Refresh()
    {
        Cards.Children.Clear();
        var query = SearchBox.Text.Trim();
        foreach (var tool in _host.Tools.OrderBy(item => item.Order))
        {
            var title = L.Get(tool.TitleKey);
            var description = L.Get(tool.DescriptionKey);
            var alias = tool.Aliases.FirstOrDefault() ?? tool.Id;
            if (query.Length > 0 && !Matches(tool, title, description, query))
                continue;

            Cards.Children.Add(BuildCard(tool, title, description, alias));
        }
    }

    private UIElement BuildCard(IKutuTool tool, string title, string description, string alias)
    {
        var icon = new SymbolIcon
        {
            Symbol = Enum.TryParse<SymbolRegular>(tool.Symbol, out var symbol) ? symbol : SymbolRegular.Circle24,
            FontSize = 22,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var bubble = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(16),
            Background = (Brush)FindResource("KutuAccentBrush"),
            Child = icon,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var name = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("KutuTextBrush"),
            Margin = new Thickness(0, 14, 0, 4)
        };
        var desc = new TextBlock
        {
            Text = description,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("KutuMutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Height = 36
        };
        var chip = new Border
        {
            Background = (Brush)FindResource("KutuFieldBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
            Child = new TextBlock
            {
                Text = alias,
                FontSize = 11,
                Foreground = (Brush)FindResource("KutuMutedBrush")
            }
        };
        var stack = new StackPanel();
        stack.Children.Add(bubble);
        stack.Children.Add(name);
        stack.Children.Add(desc);
        stack.Children.Add(chip);
        var card = new Border
        {
            Width = 248,
            Margin = new Thickness(0, 0, 14, 14),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(20),
            Background = (Brush)FindResource("KutuCardBrush"),
            BorderBrush = (Brush)FindResource("KutuLineBrush"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = stack
        };
        card.MouseEnter += (_, _) =>
        {
            card.Background = (Brush)FindResource("KutuCardHoverBrush");
            card.BorderBrush = (Brush)FindResource("KutuAccentBrush");
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = (Brush)FindResource("KutuCardBrush");
            card.BorderBrush = (Brush)FindResource("KutuLineBrush");
        };
        card.MouseLeftButtonUp += (_, _) => _host.OpenTool(tool.Id);
        return card;
    }

    private static bool Matches(IKutuTool tool, string title, string description, string query) =>
        title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || description.Contains(query, StringComparison.OrdinalIgnoreCase)
        || tool.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
        || tool.Aliases.Any(alias => alias.Contains(query, StringComparison.OrdinalIgnoreCase));
}

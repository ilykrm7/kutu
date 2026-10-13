using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.App.Views;

public sealed class LauncherWindow : Window
{
    private readonly IKutuHost _host;
    private readonly TextBox _search;
    private readonly StackPanel _results;
    private readonly List<IKutuTool> _matches = [];
    private int _selected;

    public LauncherWindow(IKutuHost host)
    {
        _host = host;
        Width = 680;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        _search = Chrome.Input(L.Get("Launcher_Placeholder"));
        _search.FontSize = 20;
        _search.Padding = new Thickness(8, 10, 8, 10);
        _search.TextChanged += (_, _) => Render();
        _search.PreviewKeyDown += OnSearchKey;
        _results = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var root = new StackPanel();
        root.Children.Add(_search);
        root.Children.Add(_results);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x0C, 0x12, 0x1E)),
            BorderBrush = new LinearGradientBrush(Color.FromRgb(0x28, 0xA0, 0xF1), Color.FromRgb(0x4B, 0xDF, 0xB3), 0),
            BorderThickness = new Thickness(1.4),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(16),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 40,
                ShadowDepth = 0,
                Opacity = 0.45,
                Color = Colors.Black
            },
            Child = root
        };
        Loaded += (_, _) =>
        {
            Render();
            _search.Focus();
        };
        Deactivated += (_, _) => Close();
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            _selected = Math.Min(_matches.Count - 1, _selected + 1);
            Render();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            _selected = Math.Max(0, _selected - 1);
            Render();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _matches.Count > 0)
        {
            Open(_matches[Math.Clamp(_selected, 0, _matches.Count - 1)]);
            e.Handled = true;
        }
    }

    private void Render()
    {
        var query = _search.Text.Trim();
        _matches.Clear();
        _matches.AddRange(_host.Tools.Where(tool => Match(tool, query)).OrderBy(tool => tool.Order));
        if (_selected >= _matches.Count)
            _selected = 0;
        _results.Children.Clear();
        if (_matches.Count == 0)
        {
            _results.Children.Add(Chrome.Hint(L.Get("Launcher_Empty")));
            return;
        }

        for (var i = 0; i < Math.Min(_matches.Count, 8); i++)
        {
            var tool = _matches[i];
            var selected = i == _selected;
            var row = new Grid { Margin = new Thickness(4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = new SymbolIcon
            {
                Symbol = Enum.TryParse<SymbolRegular>(tool.Symbol, out var symbol) ? symbol : SymbolRegular.Circle24,
                FontSize = 18,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            var title = new TextBlock
            {
                Text = L.Get(tool.TitleKey),
                Foreground = Chrome.Text,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            var alias = new TextBlock
            {
                Text = tool.Aliases.FirstOrDefault() ?? "",
                Foreground = Chrome.Muted,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 1);
            Grid.SetColumn(alias, 2);
            row.Children.Add(icon);
            row.Children.Add(title);
            row.Children.Add(alias);
            var card = new Border
            {
                Background = selected ? new SolidColorBrush(Color.FromRgb(0x1A, 0x27, 0x42)) : Brushes.Transparent,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 8, 10, 8),
                Cursor = Cursors.Hand,
                Child = row,
                Tag = tool
            };
            var index = i;
            card.MouseLeftButtonUp += (_, _) => Open(_matches[index]);
            _results.Children.Add(card);
        }
    }

    private void Open(IKutuTool tool)
    {
        Close();
        _host.OpenTool(tool.Id);
    }

    private static bool Match(IKutuTool tool, string query)
    {
        if (query.Length == 0)
            return true;

        var title = L.Get(tool.TitleKey);
        return title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tool.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || tool.Aliases.Any(alias => alias.Contains(query, StringComparison.OrdinalIgnoreCase)
                || query.Contains(alias, StringComparison.OrdinalIgnoreCase));
    }
}

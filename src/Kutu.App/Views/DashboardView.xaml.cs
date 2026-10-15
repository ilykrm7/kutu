using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kutu.Core.Hotkeys;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.App.Views;

public partial class DashboardView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _search;
    private string _filter = "all";
    private bool _favoritesOnly;

    public DashboardView(IKutuHost host)
    {
        _host = host;
        InitializeComponent();
        _search = Chrome.Input(L.Get("Dash_SearchPlaceholder"));
        _search.FontSize = 15;
        _search.Margin = new Thickness(0, 0, 0, 14);
        _search.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    private void OnPageWheel(object sender, MouseWheelEventArgs e)
    {
        PageScroller.ScrollToVerticalOffset(PageScroller.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    public void ShowAll()
    {
        _favoritesOnly = false;
        Refresh();
    }

    public void ShowFavorites()
    {
        _favoritesOnly = true;
        Refresh();
    }

    public void Refresh()
    {
        if (_search.Parent is Panel panel)
            panel.Children.Remove(_search);

        var page = new StackPanel();
        if (!_favoritesOnly)
        {
            page.Children.Add(new TextBlock
            {
                Text = L.Get("Dash_Greeting"),
                FontSize = 32,
                FontWeight = FontWeights.SemiBold,
                Foreground = Chrome.Text
            });
            page.Children.Add(new TextBlock
            {
                Text = L.Get("App_Tagline"),
                Margin = new Thickness(0, 4, 0, 16),
                FontSize = 14,
                Foreground = Chrome.Muted
            });
            page.Children.Add(_search);
            page.Children.Add(Filters());
        }
        else
        {
            page.Children.Add(Chrome.Heading(L.Get("Nav_Favorites")));
        }

        var favorites = FavoriteTools().ToList();
        if (!_favoritesOnly)
            page.Children.Add(Chrome.Heading(L.Get("Nav_Favorites")));
        if (favorites.Count == 0)
            page.Children.Add(Chrome.Hint(L.Get("Dash_FavEmpty")));
        else
            page.Children.Add(Wrap(favorites, favoritesSection: true));

        if (!_favoritesOnly)
        {
            page.Children.Add(Chrome.Heading(L.Get("Nav_Dashboard")));
            page.Children.Add(Wrap(VisibleTools().ToList(), favoritesSection: false));
        }

        var host = new PageHost { MaxContentWidth = 1080, Child = page };
        Host.Content = host;
    }

    private UIElement Filters()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        foreach (var (id, key) in new[] { ("all", "Dash_All"), ("image", "Dash_Image"), ("text", "Dash_Text"), ("net", "Dash_Net") })
        {
            var on = _filter == id;
            var button = new Button
            {
                Content = L.Get(key),
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Background = on ? new SolidColorBrush(Color.FromRgb(0x1C, 0x29, 0x44)) : Brushes.Transparent,
                Foreground = Chrome.Text,
                BorderBrush = on ? new SolidColorBrush(Color.FromRgb(0x28, 0xA0, 0xF1)) : Chrome.Line,
                BorderThickness = new Thickness(1)
            };
            button.Click += (_, _) =>
            {
                _filter = id;
                Refresh();
            };
            Motion.Pressable(button);
            row.Children.Add(button);
        }

        return row;
    }

    private WrapPanel Wrap(IReadOnlyList<IKutuTool> tools, bool favoritesSection)
    {
        var wrap = new WrapPanel();
        foreach (var tool in tools)
            wrap.Children.Add(Card(tool, favoritesSection));
        return wrap;
    }

    private IEnumerable<IKutuTool> VisibleTools()
    {
        var query = _search.Text.Trim();
        foreach (var tool in _host.Tools.OrderBy(item => item.Order))
        {
            if (_filter != "all" && Category(tool.Id) != _filter)
                continue;
            var title = L.Get(tool.TitleKey);
            var description = L.Get(tool.DescriptionKey);
            if (query.Length > 0 && !Matches(tool, title, description, query))
                continue;
            yield return tool;
        }
    }

    private IEnumerable<IKutuTool> FavoriteTools()
    {
        var query = _favoritesOnly ? "" : _search.Text.Trim();
        foreach (var id in _host.Settings.FavoriteTools)
        {
            var tool = _host.Tools.FirstOrDefault(item => item.Id == id);
            if (tool is null)
                continue;
            if (query.Length > 0 && !Matches(tool, L.Get(tool.TitleKey), L.Get(tool.DescriptionKey), query))
                continue;
            yield return tool;
        }
    }

    private UIElement Card(IKutuTool tool, bool favoritesSection)
    {
        var soon = tool.Id == "upload";
        var title = L.Get(tool.TitleKey);
        var description = L.Get(tool.DescriptionKey);
        var tint = Tint(tool.Id);
        var icon = new SymbolIcon
        {
            Symbol = Chrome.Symbol(tool.Symbol),
            FontSize = 22,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var bubble = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(tint),
            Child = icon
        };
        var pinned = _host.Settings.FavoriteTools.Contains(tool.Id);
        var pin = new Button
        {
            Content = pinned ? "★" : "☆",
            ToolTip = pinned ? L.Get("Dash_Unpin") : L.Get("Dash_Pin"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(tint),
            FontSize = 16,
            Cursor = Cursors.Hand,
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        pin.Click += async (_, e) =>
        {
            e.Handled = true;
            ToggleFavorite(tool.Id);
            await _host.SaveSettingsAsync();
            Refresh();
        };

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(bubble);
        Grid.SetColumn(pin, 1);
        top.Children.Add(pin);

        var stack = new StackPanel();
        stack.Children.Add(top);
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Chrome.Text,
            Margin = new Thickness(0, 12, 0, 4)
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12.5,
            Foreground = Chrome.Muted,
            TextWrapping = TextWrapping.Wrap,
            Height = 36
        });

        var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var badge = soon ? L.Get("Dash_Soon") : ToolHotkeyMap.Get(_host.Settings, tool.Id);
        if (!string.IsNullOrWhiteSpace(badge))
            foot.Children.Add(Chip(badge));
        if (tool.Id is "text-expander" or "clipboard")
        {
            var toggle = new ToggleSwitch
            {
                Margin = new Thickness(8, 0, 0, 0),
                IsChecked = tool.Id == "clipboard" ? _host.Settings.ClipboardHistoryEnabled : _host.Settings.TextExpanderEnabled
            };
            toggle.Checked += async (_, _) => await SetEnabled(tool.Id, true);
            toggle.Unchecked += async (_, _) => await SetEnabled(tool.Id, false);
            foot.Children.Add(toggle);
        }

        stack.Children.Add(foot);
        if (favoritesSection)
        {
            var grip = new TextBlock
            {
                Text = "⋮⋮",
                Foreground = Chrome.Muted,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = Cursors.SizeAll,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            grip.MouseMove += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    DragDrop.DoDragDrop(grip, tool.Id, DragDropEffects.Move);
            };
            stack.Children.Add(grip);
        }

        var card = new Border
        {
            Width = 248,
            Margin = new Thickness(0, 0, 14, 14),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18),
            Background = Chrome.CardBrush,
            BorderBrush = Chrome.Line,
            BorderThickness = new Thickness(1),
            Cursor = soon ? Cursors.Arrow : Cursors.Hand,
            Opacity = soon ? 0.55 : 1,
            Child = stack,
            AllowDrop = favoritesSection
        };
        if (!soon)
        {
            Motion.HoverLift(card);
            card.MouseLeftButtonUp += (_, e) =>
            {
                if (Inside<Button>(e.OriginalSource as DependencyObject) || Inside<ToggleSwitch>(e.OriginalSource as DependencyObject))
                    return;
                _host.OpenTool(tool.Id);
            };
        }

        if (favoritesSection)
        {
            card.Drop += async (_, e) =>
            {
                if (e.Data.GetData(typeof(string)) is not string source || source == tool.Id)
                    return;
                Reorder(source, tool.Id);
                await _host.SaveSettingsAsync();
                Refresh();
            };
        }

        return card;
    }

    private async Task SetEnabled(string id, bool enabled)
    {
        if (id == "clipboard")
        {
            _host.Settings.ClipboardHistoryEnabled = enabled;
            _host.Clipboard.Enabled = enabled;
        }
        else
        {
            _host.Settings.TextExpanderEnabled = enabled;
        }

        await _host.SaveSettingsAsync();
    }

    private void ToggleFavorite(string id)
    {
        var list = _host.Settings.FavoriteTools;
        if (!list.Remove(id))
            list.Add(id);
    }

    private void Reorder(string sourceId, string targetId)
    {
        var list = _host.Settings.FavoriteTools;
        var from = list.IndexOf(sourceId);
        var to = list.IndexOf(targetId);
        if (from < 0 || to < 0 || from == to)
            return;
        list.RemoveAt(from);
        list.Insert(to, sourceId);
    }

    private static bool Inside<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T)
                return true;
            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private static Border Chip(string text) => new()
    {
        Background = Chrome.Field,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(8, 3, 8, 3),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = text, FontSize = 11, Foreground = Chrome.Muted }
    };

    private static string Category(string id) => id switch
    {
        "image-converter" or "image-resize" or "screenshot" or "color-picker" or "qr" or "screen-record" or "video" => "image",
        "clipboard" or "text-expander" or "translate" or "ocr" or "screen-translate" or "password" => "text",
        _ => "net"
    };

    private static Color Tint(string id) => id switch
    {
        "image-converter" => Color.FromRgb(0x28, 0xA0, 0xF1),
        "image-resize" => Color.FromRgb(0x3B, 0xC6, 0xE0),
        "screenshot" => Color.FromRgb(0x4B, 0xDF, 0xB3),
        "color-picker" => Color.FromRgb(0x5B, 0x8D, 0xEF),
        "clipboard" => Color.FromRgb(0x2E, 0xC4, 0xB6),
        "text-expander" => Color.FromRgb(0x3A, 0x9B, 0xE0),
        "translate" => Color.FromRgb(0x3D, 0xD6, 0xC6),
        "qr" => Color.FromRgb(0x7C, 0x6C, 0xF0),
        "screen-record" => Color.FromRgb(0xFF, 0x6B, 0x8A),
        "video" => Color.FromRgb(0xF0, 0xA0, 0x5A),
        "ocr" => Color.FromRgb(0x5A, 0xD0, 0xA0),
        "screen-translate" => Color.FromRgb(0x2B, 0xB8, 0xA0),
        "password" => Color.FromRgb(0xC6, 0xA1, 0x5B),
        "whois" => Color.FromRgb(0x49, 0xC6, 0xA8),
        "ip-reputation" => Color.FromRgb(0x28, 0xB4, 0xE6),
        "bulk-ip" => Color.FromRgb(0x49, 0xA0, 0xE6),
        "net-probe" => Color.FromRgb(0x3B, 0xC6, 0xE0),
        "ssl" => Color.FromRgb(0x6B, 0xCB, 0x77),
        _ => Color.FromRgb(0x6A, 0x7A, 0x94)
    };

    private static bool Matches(IKutuTool tool, string title, string description, string query) =>
        title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        || description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        || tool.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        || tool.Aliases.Any(alias => alias.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
}

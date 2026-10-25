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
        _search.Margin = new Thickness(0, 0, 0, 16);
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
        page.Children.Add(new TextBlock
        {
            Text = L.Get(_favoritesOnly ? "Nav_Favorites" : "Nav_Dashboard"),
            FontSize = 34,
            FontWeight = FontWeights.SemiBold,
            Foreground = Chrome.Text,
            Margin = new Thickness(0, 8, 0, 0)
        });
        if (!_favoritesOnly)
        {
            page.Children.Add(new TextBlock
            {
                Text = L.Get("App_Tagline"),
                Margin = new Thickness(0, 6, 0, 18),
                FontSize = 15,
                Foreground = Chrome.Muted
            });
            page.Children.Add(_search);
            page.Children.Add(Filters());
        }
        else
        {
            page.Children.Add(new Border { Height = 16 });
        }

        var favorites = FavoriteTools().ToList();
        if (favorites.Count == 0)
        {
            if (_favoritesOnly)
                page.Children.Add(Chrome.Hint(L.Get("Dash_FavEmpty")));
        }
        else
        {
            if (!_favoritesOnly)
                page.Children.Add(Chrome.Heading(L.Get("Nav_Favorites")));
            page.Children.Add(Wrap(favorites, favoritesSection: true));
        }

        if (!_favoritesOnly)
            page.Children.Add(Wrap(VisibleTools().ToList(), favoritesSection: false));

        var host = new PageHost { MaxContentWidth = 1080, Child = page };
        Host.Content = host;
    }

    private UIElement Filters()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var shell = new Border
        {
            Background = Chrome.CardBrush,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 18),
            Child = row
        };
        foreach (var (id, key) in new[] { ("all", "Dash_All"), ("image", "Dash_Image"), ("text", "Dash_Text"), ("net", "Dash_Net") })
        {
            var on = _filter == id;
            var chip = new Border
            {
                Background = on ? new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)) : Brushes.Transparent,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = L.Get(key),
                    Foreground = on ? Brushes.White : Chrome.Muted,
                    FontSize = 13,
                    FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal
                }
            };
            var chosen = id;
            chip.MouseLeftButtonUp += (_, _) =>
            {
                _filter = chosen;
                Refresh();
            };
            row.Children.Add(chip);
        }

        return shell;
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
            FontSize = 13,
            Foreground = Chrome.Muted,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 40
        });

        var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var badge = ToolHotkeyMap.Get(_host.Settings, tool.Id);
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
            Width = 252,
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18),
            Background = Chrome.CardBrush,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Child = stack,
            AllowDrop = favoritesSection
        };
        Motion.HoverLift(card);
        card.MouseLeftButtonUp += (_, e) =>
        {
            if (Inside<Button>(e.OriginalSource as DependencyObject) || Inside<ToggleSwitch>(e.OriginalSource as DependencyObject))
                return;
            _host.OpenTool(tool.Id);
        };

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
        "image-converter" or "image-resize" or "matte" or "screenshot" or "color-picker" or "qr" or "screen-record" or "video" => "image",
        "clipboard" or "text-expander" or "translate" or "ocr" or "screen-translate" or "password" or "fonts" or "uuid" or "digest" => "text",
        _ => "net"
    };

    private static Color Tint(string id) => id switch
    {
        "image-converter" => Color.FromRgb(0x0A, 0x84, 0xFF),
        "image-resize" => Color.FromRgb(0x64, 0xD2, 0xFF),
        "matte" => Color.FromRgb(0xBF, 0x5A, 0xF2),
        "screenshot" => Color.FromRgb(0x30, 0xD1, 0x58),
        "color-picker" => Color.FromRgb(0x5E, 0x5C, 0xE6),
        "fonts" => Color.FromRgb(0xFF, 0x9F, 0x0A),
        "clipboard" => Color.FromRgb(0x64, 0xD2, 0xFF),
        "text-expander" => Color.FromRgb(0x0A, 0x84, 0xFF),
        "translate" => Color.FromRgb(0x30, 0xD1, 0x58),
        "qr" => Color.FromRgb(0x5E, 0x5C, 0xE6),
        "screen-record" => Color.FromRgb(0xFF, 0x37, 0x5F),
        "video" => Color.FromRgb(0xFF, 0x9F, 0x0A),
        "ocr" => Color.FromRgb(0x30, 0xD1, 0x58),
        "screen-translate" => Color.FromRgb(0x64, 0xD2, 0xFF),
        "password" => Color.FromRgb(0xFF, 0xD6, 0x0A),
        "uuid" => Color.FromRgb(0xBF, 0x5A, 0xF2),
        "digest" => Color.FromRgb(0x5E, 0x5C, 0xE6),
        "whois" => Color.FromRgb(0x30, 0xD1, 0x58),
        "ip-reputation" => Color.FromRgb(0x0A, 0x84, 0xFF),
        "bulk-ip" => Color.FromRgb(0x64, 0xD2, 0xFF),
        "net-probe" => Color.FromRgb(0x5E, 0x5C, 0xE6),
        "ssl" => Color.FromRgb(0x30, 0xD1, 0x58),
        "upload" => Color.FromRgb(0x0A, 0x84, 0xFF),
        _ => Color.FromRgb(0x8E, 0x8E, 0x93)
    };

    private static bool Matches(IKutuTool tool, string title, string description, string query) =>
        title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        || description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        || tool.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        || tool.Aliases.Any(alias => alias.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
}

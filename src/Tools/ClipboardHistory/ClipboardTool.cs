using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Models;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.ClipboardHistory;

public sealed class ClipboardTool : IKutuTool
{
    public string Id => "clipboard";

    public string TitleKey => "Tool_Clip_Title";

    public string DescriptionKey => "Tool_Clip_Desc";

    public string Symbol => "Clipboard24";

    public IReadOnlyList<string> Aliases { get; } = ["clip", "pano", "clipboard", "kopya", "paste", "history"];

    public int Order => 50;

    public UIElement CreateView(IKutuHost host) => new ClipboardView(host);
}

public sealed class ClipboardView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _search;
    private readonly StackPanel _list;

    public ClipboardView(IKutuHost host)
    {
        _host = host;
        _search = Chrome.Input(L.Get("Clip_Search"));
        _search.TextChanged += (_, _) => Render();
        _list = new StackPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Clear"), SymbolRegular.Dismiss24, (_, _) =>
        {
            host.Clipboard.ClearUnpinned();
            host.Toast(L.Get("Clip_Cleared"));
        }));
        var stack = new StackPanel();
        stack.Children.Add(_search);
        stack.Children.Add(new Border { Height = 10 });
        stack.Children.Add(buttons);
        stack.Children.Add(_list);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        host.Clipboard.Changed += (_, _) => Dispatcher.Invoke(Render);
        Render();
    }

    private void Render() => ClipboardUi.Render(_host, _list, _search.Text, popup: false);
}

public static class ClipboardPopup
{
    public static void Show(IKutuHost host)
    {
        var window = new Window
        {
            Width = 640,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };
        var search = Chrome.Input(L.Get("Clip_Search"));
        var list = new StackPanel();
        search.TextChanged += (_, _) => ClipboardUi.Render(host, list, search.Text, popup: true, window);
        var stack = new DockPanel();
        DockPanel.SetDock(search, Dock.Top);
        search.Margin = new Thickness(0, 0, 0, 10);
        stack.Children.Add(search);
        stack.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        window.Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x10, 0x18, 0x28)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0xA0, 0xF1)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(16),
            Child = stack
        };
        window.Deactivated += (_, _) => window.Close();
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                window.Close();
        };
        ClipboardUi.Render(host, list, "", popup: true, window);
        window.Show();
        search.Focus();
    }
}

public static class ClipboardUi
{
    public static void Render(IKutuHost host, StackPanel list, string query, bool popup, Window? window = null)
    {
        list.Children.Clear();
        var items = host.Clipboard.Items.Where(item => Matches(item, query)).Take(50).ToList();
        if (items.Count == 0)
        {
            list.Children.Add(Chrome.Hint(L.Get("Clip_Empty")));
            return;
        }

        foreach (var item in items)
            list.Children.Add(Row(host, item, popup, window));
    }

    private static bool Matches(ClipItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        return item.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static UIElement Row(IKutuHost host, ClipItem item, bool popup, Window? window)
    {
        var preview = item.Kind == "image" ? "PNG" : OneLine(item.Text);
        var text = new TextBlock
        {
            Text = preview,
            Foreground = Chrome.Text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 14
        };
        var pin = Chrome.Secondary(item.Pinned ? L.Get("Action_Unpin") : L.Get("Action_Pin"), item.Pinned ? SymbolRegular.PinOff24 : SymbolRegular.Pin24, (_, _) => host.Clipboard.TogglePin(item.Id));
        var del = Chrome.Secondary(L.Get("Action_Delete"), SymbolRegular.Delete24, (_, _) => host.Clipboard.Remove(item.Id));
        var copy = Chrome.Primary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) =>
        {
            host.Clipboard.Copy(item);
            host.Toast(L.Get("Action_Copied"));
            if (popup)
                window?.Close();
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(pin);
        buttons.Children.Add(del);
        buttons.Children.Add(copy);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (item.Kind == "image" && item.ImagePath is not null && File.Exists(item.ImagePath))
        {
            var image = new Image
            {
                Source = Load(item.ImagePath),
                Width = 72,
                Height = 48,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(image);
            row.Children.Add(text);
            grid.Children.Add(row);
        }
        else
        {
            grid.Children.Add(text);
        }

        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);
        var card = Chrome.Card(grid, new Thickness(12));
        card.Margin = new Thickness(0, 0, 0, 8);
        return card;
    }

    private static string OneLine(string text)
    {
        var line = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return line.Length > 180 ? line[..180] : line;
    }

    private static BitmapImage Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.DecodePixelWidth = 144;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

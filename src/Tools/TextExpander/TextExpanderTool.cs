using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Models;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.TextExpander;

public sealed class TextExpanderTool : IKutuTool
{
    public string Id => "text-expander";

    public string TitleKey => "Tool_Expand_Title";

    public string DescriptionKey => "Tool_Expand_Desc";

    public string Symbol => "Keyboard24";

    public IReadOnlyList<string> Aliases { get; } = ["expand", "snippet", "imza", "espanso", "metin", "paste", ":paste"];

    public int Order => 60;

    public UIElement CreateView(IKutuHost host) => new TextExpanderView(host);
}

public sealed class TextExpanderView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _trigger;
    private readonly TextBox _body;
    private readonly StackPanel _list;
    private readonly TextBlock _status;

    public TextExpanderView(IKutuHost host)
    {
        _host = host;
        _trigger = Chrome.Input(":imza");
        _trigger.Width = 220;
        _body = Chrome.Input(L.Get("Expand_Body"), multiline: true);
        _list = new StackPanel();
        _status = Chrome.Hint("");
        var add = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var triggerField = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        triggerField.Children.Add(Chrome.Label(L.Get("Expand_Trigger")));
        triggerField.Children.Add(_trigger);
        add.Children.Add(triggerField);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Add"), SymbolRegular.Add24, async (_, _) => await AddAsync()));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Expand_Hint")));
        stack.Children.Add(add);
        stack.Children.Add(Chrome.Label(L.Get("Expand_Body")));
        stack.Children.Add(_body);
        stack.Children.Add(new Border { Height = 8 });
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        stack.Children.Add(_list);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Render();
    }

    private async Task AddAsync()
    {
        var trigger = _trigger.Text.Trim();
        var body = _body.Text;
        if (trigger.Length == 0 || string.IsNullOrWhiteSpace(body))
        {
            _status.Text = L.Get("Expand_Invalid");
            return;
        }

        _host.Settings.Snippets.RemoveAll(x => x.Trigger.Equals(trigger, StringComparison.Ordinal));
        _host.Settings.Snippets.Add(new Snippet { Trigger = trigger, Body = body });
        await _host.SaveSettingsAsync();
        _trigger.Text = "";
        _body.Text = "";
        _status.Text = L.Get("Expand_Saved");
        Render();
    }

    private void Render()
    {
        _list.Children.Clear();
        foreach (var snippet in _host.Settings.Snippets.ToList())
        {
            var title = new TextBlock
            {
                Text = snippet.Trigger,
                Foreground = Chrome.Text,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            var body = new TextBlock
            {
                Text = snippet.Body,
                Foreground = Chrome.Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            var text = new StackPanel();
            text.Children.Add(title);
            text.Children.Add(body);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(text);
            var remove = Chrome.Secondary(L.Get("Action_Delete"), SymbolRegular.Delete24, async (_, _) =>
            {
                _host.Settings.Snippets.RemoveAll(x => x.Id == snippet.Id);
                await _host.SaveSettingsAsync();
                Render();
            });
            Grid.SetColumn(remove, 1);
            remove.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(remove);
            var card = Chrome.Card(grid, new Thickness(14));
            card.Margin = new Thickness(0, 10, 0, 0);
            _list.Children.Add(card);
        }
    }
}

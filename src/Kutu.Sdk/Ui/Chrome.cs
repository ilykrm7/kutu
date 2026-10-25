using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Sdk.Ui;

public static class Chrome
{
    public static Brush Accent => Brush("KutuAccentBrush");

    public static Brush CardBrush => Brush("KutuCardBrush");

    public static Brush CardHover => Brush("KutuCardHoverBrush");

    public static Brush Line => Brush("KutuLineBrush");

    public static Brush Text => Brush("KutuTextBrush");

    public static Brush Muted => Brush("KutuMutedBrush");

    public static Brush Field => Brush("KutuFieldBrush");

    public static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        Foreground = Text,
        Margin = new Thickness(0, 0, 0, 8)
    };

    public static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = Muted,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 14),
        LineHeight = 20
    };

    public static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = Muted,
        Margin = new Thickness(2, 0, 0, 6)
    };

    public static Border Card(UIElement child, Thickness? padding = null) => new()
    {
        Background = CardBrush,
        BorderBrush = Line,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(16),
        Padding = padding ?? new Thickness(18),
        Child = child
    };

    public static Button Primary(string text, SymbolRegular symbol, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            Appearance = ControlAppearance.Primary,
            Icon = new SymbolIcon { Symbol = symbol, FontSize = 16 },
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 8, 0),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13
        };
        button.Click += click;
        Motion.Pressable(button);
        return button;
    }

    public static Button Secondary(string text, SymbolRegular symbol, RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            Appearance = ControlAppearance.Secondary,
            Icon = new SymbolIcon { Symbol = symbol, FontSize = 16 },
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0)
        };
        button.Click += click;
        Motion.Pressable(button);
        return button;
    }

    public static TextBox Input(string placeholder, bool multiline = false)
    {
        var box = new QuietTextBox
        {
            PlaceholderText = placeholder,
            Padding = new Thickness(12, 8, 12, 8),
            FontSize = 14,
            ClearButtonEnabled = false
        };
        if (multiline)
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 120;
            box.VerticalContentAlignment = VerticalAlignment.Top;
        }

        return box;
    }

    public static Border DropZone(string text, out TextBlock label)
    {
        label = new TextBlock
        {
            Text = text,
            Foreground = Muted,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24)
        };
        return new Border
        {
            MinHeight = 148,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Line,
            Background = Field,
            Child = label,
            AllowDrop = true,
            Margin = new Thickness(0, 0, 0, 14)
        };
    }

    public static Button Soon(string text, SymbolRegular symbol)
    {
        var button = new Button
        {
            Content = text,
            Appearance = ControlAppearance.Secondary,
            Icon = new SymbolIcon { Symbol = symbol, FontSize = 16 },
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            IsEnabled = false,
            ToolTip = Localization.L.Get("Dash_Soon")
        };
        return button;
    }

    public static UIElement ValueRow(string label, string value, Action? copy = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = Label(label);
        name.Margin = new Thickness(0);
        name.VerticalAlignment = VerticalAlignment.Center;
        var text = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            Foreground = Text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(name);
        grid.Children.Add(text);
        if (copy is not null && !string.IsNullOrWhiteSpace(value))
        {
            var button = Secondary("", SymbolRegular.Copy24, (_, _) => copy());
            button.Padding = new Thickness(8, 4, 8, 4);
            button.Margin = new Thickness(8, 0, 0, 0);
            button.ToolTip = Localization.L.Get("Action_Copy");
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);
        }

        return grid;
    }

    public static Border Section(string title, SymbolRegular symbol, UIElement body)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 0, 0, 8) };
        header.Children.Add(new SymbolIcon
        {
            Symbol = symbol,
            FontSize = 14,
            Foreground = Muted,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Muted,
            VerticalAlignment = VerticalAlignment.Center
        });
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        stack.Children.Add(header);
        var card = Card(body);
        card.Margin = new Thickness(0);
        stack.Children.Add(card);
        return new Border { Child = stack };
    }

    public static void BubbleWheel(UIElement element)
    {
        element.PreviewMouseWheel += (_, e) =>
        {
            if (FindScrollViewer(element) is not ScrollViewer viewer)
                return;

            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
            e.Handled = true;
        };
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject start)
    {
        var node = VisualTreeHelper.GetParent(start);
        while (node is not null)
        {
            if (node is ScrollViewer viewer)
                return viewer;
            node = VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    public static ScrollViewer ScrollPage(UIElement content, double maxWidth = 880)
    {
        var host = new PageHost { MaxContentWidth = maxWidth, Child = content };
        return new ScrollViewer
        {
            Content = host,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    public static UIElement Loading(string text)
    {
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = 18,
            Height = 18,
            StrokeThickness = 2.2,
            Stroke = Accent,
            StrokeDashArray = new DoubleCollection { 2.2, 1.6 },
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform()
        };
        ((RotateTransform)ring.RenderTransform).BeginAnimation(
            RotateTransform.AngleProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(700))
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        row.Children.Add(ring);
        row.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Muted,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        return row;
    }

    public static SymbolRegular Symbol(string name) =>
        Enum.TryParse<SymbolRegular>(name, out var symbol) ? symbol : SymbolRegular.Circle24;

    private sealed class QuietTextBox : TextBox
    {
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            var caret = CaretIndex;
            var length = Text?.Length ?? 0;
            base.OnGotFocus(e);
            if (length > 0)
                CaretIndex = Math.Min(Math.Max(caret, 0), length);
        }
    }

    private static Brush Brush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;

        return key switch
        {
            "KutuAccentBrush" => new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)),
            "KutuMutedBrush" => new SolidColorBrush(Color.FromRgb(0x98, 0x98, 0x9D)),
            "KutuFieldBrush" => new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1E)),
            "KutuLineBrush" => new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x3A)),
            _ => new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E))
        };
    }
}

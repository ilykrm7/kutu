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
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(18),
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
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 8, 0),
            FontWeight = FontWeights.SemiBold
        };
        button.Click += click;
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
        return button;
    }

    public static TextBox Input(string placeholder, bool multiline = false)
    {
        var box = new TextBox
        {
            PlaceholderText = placeholder,
            Padding = new Thickness(12, 8, 12, 8),
            FontSize = 14
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
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1.5),
            BorderBrush = Line,
            Background = Field,
            Child = label,
            AllowDrop = true,
            Margin = new Thickness(0, 0, 0, 14)
        };
    }

    public static SymbolRegular Symbol(string name) =>
        Enum.TryParse<SymbolRegular>(name, out var symbol) ? symbol : SymbolRegular.Circle24;

    private static Brush Brush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;

        return key switch
        {
            "KutuAccentBrush" => new LinearGradientBrush(Color.FromRgb(0x28, 0xA0, 0xF1), Color.FromRgb(0x4B, 0xDF, 0xB3), 45),
            "KutuMutedBrush" => new SolidColorBrush(Color.FromRgb(0x93, 0xA0, 0xB8)),
            "KutuFieldBrush" => new SolidColorBrush(Color.FromRgb(0x0E, 0x15, 0x26)),
            "KutuLineBrush" => new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x58)),
            _ => new SolidColorBrush(Color.FromRgb(0x14, 0x1C, 0x2E))
        };
    }
}

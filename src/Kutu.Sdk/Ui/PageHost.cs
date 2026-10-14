using System.Windows;
using System.Windows.Controls;

namespace Kutu.Sdk.Ui;

public sealed class PageHost : Decorator
{
    public double MaxContentWidth { get; set; } = 1080;

    public Thickness PagePadding { get; set; } = new(24, 4, 36, 40);

    protected override Size MeasureOverride(Size constraint)
    {
        var width = double.IsInfinity(constraint.Width) ? MaxContentWidth + PagePadding.Left + PagePadding.Right : constraint.Width;
        var inner = Math.Max(0, Math.Min(MaxContentWidth, width - PagePadding.Left - PagePadding.Right));
        Child?.Measure(new Size(inner, double.PositiveInfinity));
        var height = (Child?.DesiredSize.Height ?? 0) + PagePadding.Top + PagePadding.Bottom;
        return new Size(Math.Max(0, width), height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var inner = Math.Max(0, Math.Min(MaxContentWidth, arrangeSize.Width - PagePadding.Left - PagePadding.Right));
        var x = PagePadding.Left + Math.Max(0, (arrangeSize.Width - PagePadding.Left - PagePadding.Right - inner) / 2);
        Child?.Arrange(new Rect(x, PagePadding.Top, inner, Math.Max(0, Child?.DesiredSize.Height ?? 0)));
        return arrangeSize;
    }
}

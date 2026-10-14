using Kutu.Core;

namespace Kutu.Core.Geometry;

public readonly record struct ShotBarSlot(double X, double Y)
{
    public const double Margin = 8;

    public const double Gap = 10;

    public static double WidthLimit(double monitorWidth, double chrome = 0, double margin = Margin) =>
        Math.Max(0, monitorWidth - (margin * 2) - Math.Max(0, chrome));

    public static ShotBarSlot Place(
        double selectionX,
        double selectionY,
        double selectionWidth,
        double selectionHeight,
        double barWidth,
        double barHeight,
        double monitorX,
        double monitorY,
        double monitorWidth,
        double monitorHeight,
        double margin = Margin,
        double gap = Gap)
    {
        var innerLeft = monitorX + margin;
        var innerTop = monitorY + margin;
        var innerRight = monitorX + monitorWidth - margin;
        var innerBottom = monitorY + monitorHeight - margin;
        if (innerRight < innerLeft)
            innerRight = innerLeft;
        if (innerBottom < innerTop)
            innerBottom = innerTop;

        var width = Math.Max(0, barWidth);
        var height = Math.Max(0, barHeight);
        var left = selectionX;
        if (left + width > innerRight)
            left = innerRight - width;
        if (left < innerLeft)
            left = innerLeft;

        var below = selectionY + selectionHeight + gap;
        var above = selectionY - gap - height;
        double top;
        if (below + height <= innerBottom)
            top = below;
        else if (above >= innerTop)
            top = above;
        else
        {
            var inside = selectionY + selectionHeight - height - gap;
            var floor = innerBottom - height;
            if (floor < innerTop)
                floor = innerTop;
            top = Num.Clamp(inside, innerTop, floor);
        }

        return new ShotBarSlot(left, top);
    }
}

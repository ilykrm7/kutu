using Kutu.Core;

namespace Kutu.Core.Geometry;

public readonly record struct ShotDrag(double X, double Y, double Width, double Height)
{
    public bool HasExtent(double minimum = 2) => Width >= minimum || Height >= minimum;

    public static ShotDrag FromPoints(double x1, double y1, double x2, double y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

    public ShotDrag Clamp(double width, double height)
    {
        var limitW = Math.Max(0, width);
        var limitH = Math.Max(0, height);
        var left = Num.Clamp(X, 0, limitW);
        var top = Num.Clamp(Y, 0, limitH);
        var right = Num.Clamp(X + Width, 0, limitW);
        var bottom = Num.Clamp(Y + Height, 0, limitH);
        return new ShotDrag(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public static (double X, double Y) ClampPoint(double x, double y, double width, double height) =>
        (Num.Clamp(x, 0, Math.Max(0, width)), Num.Clamp(y, 0, Math.Max(0, height)));
}

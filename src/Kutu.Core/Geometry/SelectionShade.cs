namespace Kutu.Core.Geometry;

/// <summary>
/// DIP bands for a snip overlay: a full-window dim, or four bands around an undimmed hole.
/// </summary>
public static class SelectionShade
{
    public readonly record struct Band(double X, double Y, double Width, double Height);

    public static Band[] Cover(double width, double height)
    {
        var w = Math.Max(0, width);
        var h = Math.Max(0, height);
        return new[]
        {
            new Band(0, 0, w, h),
            new Band(0, 0, 0, 0),
            new Band(0, 0, 0, 0),
            new Band(0, 0, 0, 0)
        };
    }

    public static Band[] Around(double width, double height, double x, double y, double selWidth, double selHeight)
    {
        var w = Math.Max(0, width);
        var h = Math.Max(0, height);
        var left = Clamp(x, 0, w);
        var top = Clamp(y, 0, h);
        var right = Clamp(x + Math.Max(0, selWidth), left, w);
        var bottom = Clamp(y + Math.Max(0, selHeight), top, h);
        return new[]
        {
            new Band(0, 0, w, top),
            new Band(0, top, left, bottom - top),
            new Band(right, top, w - right, bottom - top),
            new Band(0, bottom, w, h - bottom)
        };
    }

    private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
}

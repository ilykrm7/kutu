namespace Kutu.Core.Geometry;

public static class MonitorLayout
{
    public readonly record struct Input(int X, int Y, int Width, int Height, double DpiX, double DpiY, bool Primary);

    public readonly record struct Band(int X, int Y, int Width, int Height, double DpiX, double DpiY, double DipX, double DipY, double DipWidth, double DipHeight);

    public static IReadOnlyList<Band> Arrange(IReadOnlyList<Input> monitors)
    {
        if (monitors is null || monitors.Count == 0)
            return [];

        var primary = 0;
        for (var i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].Primary && monitors[i].Width > 0 && monitors[i].Height > 0)
            {
                primary = i;
                break;
            }
        }

        var placed = new Band?[monitors.Count];
        placed[primary] = BandOf(monitors[primary], 0, 0);
        var guard = 0;
        var changed = true;
        while (changed && guard++ < monitors.Count * monitors.Count)
        {
            changed = false;
            for (var i = 0; i < monitors.Count; i++)
            {
                if (placed[i] is not null || monitors[i].Width <= 0 || monitors[i].Height <= 0)
                    continue;
                for (var j = 0; j < monitors.Count; j++)
                {
                    if (placed[j] is not Band anchor)
                        continue;
                    if (!TryOffset(monitors[j], anchor, monitors[i], out var dipX, out var dipY))
                        continue;
                    placed[i] = BandOf(monitors[i], dipX, dipY);
                    changed = true;
                    break;
                }
            }
        }

        var primaryInput = monitors[primary];
        var primaryDpiX = Dpi(primaryInput.DpiX);
        var primaryDpiY = Dpi(primaryInput.DpiY);
        for (var i = 0; i < monitors.Count; i++)
        {
            if (placed[i] is not null)
                continue;
            var dipX = (monitors[i].X - primaryInput.X) * 96d / primaryDpiX;
            var dipY = (monitors[i].Y - primaryInput.Y) * 96d / primaryDpiY;
            placed[i] = BandOf(monitors[i], dipX, dipY);
        }

        var list = new List<Band>(monitors.Count);
        foreach (var band in placed)
        {
            if (band is Band value && value.Width > 0 && value.Height > 0)
                list.Add(value);
        }

        return list;
    }

    public static bool Matches(IReadOnlyList<Band> bands, double dipX, double dipY, double dipWidth, double dipHeight)
    {
        if (bands.Count == 0 || dipWidth <= 0 || dipHeight <= 0)
            return false;
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        foreach (var band in bands)
        {
            if (band.DipX < left)
                left = band.DipX;
            if (band.DipY < top)
                top = band.DipY;
            if (band.DipX + band.DipWidth > right)
                right = band.DipX + band.DipWidth;
            if (band.DipY + band.DipHeight > bottom)
                bottom = band.DipY + band.DipHeight;
        }

        return Math.Abs(left - dipX) <= 4
            && Math.Abs(top - dipY) <= 4
            && Math.Abs(right - left - dipWidth) <= 4
            && Math.Abs(bottom - top - dipHeight) <= 4;
    }

    public static (int X, int Y, int Width, int Height) PhysicalBounds(double dipX, double dipY, double dipWidth, double dipHeight, IReadOnlyList<Band> bands)
    {
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;
        var any = false;
        foreach (var band in bands)
        {
            var x1 = Math.Max(dipX, band.DipX);
            var y1 = Math.Max(dipY, band.DipY);
            var x2 = Math.Min(dipX + dipWidth, band.DipX + band.DipWidth);
            var y2 = Math.Min(dipY + dipHeight, band.DipY + band.DipHeight);
            if (x2 - x1 < 0.5 || y2 - y1 < 0.5)
                continue;
            any = true;
            var px1 = band.X + (int)Math.Round((x1 - band.DipX) * band.DpiX / 96d);
            var py1 = band.Y + (int)Math.Round((y1 - band.DipY) * band.DpiY / 96d);
            var px2 = band.X + (int)Math.Round((x2 - band.DipX) * band.DpiX / 96d);
            var py2 = band.Y + (int)Math.Round((y2 - band.DipY) * band.DpiY / 96d);
            if (px1 < left)
                left = px1;
            if (py1 < top)
                top = py1;
            if (px2 > right)
                right = px2;
            if (py2 > bottom)
                bottom = py2;
        }

        if (!any)
            return (0, 0, 1, 1);
        return (left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    public static (double X, double Y, double Width, double Height) UnderLine(double physX, double physY, double physWidth, double physHeight, double windowDipX, double windowDipY, IReadOnlyList<Band> bands)
    {
        foreach (var band in bands)
        {
            if (physX < band.X || physX >= band.X + band.Width || physY < band.Y || physY >= band.Y + band.Height)
                continue;
            var x = band.DipX + (physX - band.X) * 96d / band.DpiX - windowDipX;
            var y = band.DipY + (physY - band.Y) * 96d / band.DpiY - windowDipY;
            var width = physWidth * 96d / band.DpiX;
            var height = physHeight * 96d / band.DpiY;
            return (x, y + height, width, height);
        }

        return (0, 0, 0, 0);
    }

    public static (double X, double Y, double Width) LinearUnder(double lineX, double lineY, double lineWidth, double lineHeight, int pixelWidth, int pixelHeight, double dipWidth, double dipHeight)
    {
        var scaleX = dipWidth / Math.Max(1, pixelWidth);
        var scaleY = dipHeight / Math.Max(1, pixelHeight);
        return (lineX * scaleX, (lineY + lineHeight) * scaleY, Math.Max(72, lineWidth * scaleX));
    }

    private static bool TryOffset(Input anchorIn, Band anchor, Input other, out double dipX, out double dipY)
    {
        dipX = 0;
        dipY = 0;
        var vertical = Math.Min(anchorIn.Y + anchorIn.Height, other.Y + other.Height) - Math.Max(anchorIn.Y, other.Y);
        var horizontal = Math.Min(anchorIn.X + anchorIn.Width, other.X + other.Width) - Math.Max(anchorIn.X, other.X);
        if (other.X == anchorIn.X + anchorIn.Width && vertical > 0)
        {
            dipX = anchor.DipX + anchor.DipWidth;
            dipY = anchor.DipY + (other.Y - anchorIn.Y) * 96d / Dpi(anchor.DpiY);
            return true;
        }

        if (anchorIn.X == other.X + other.Width && vertical > 0)
        {
            dipX = anchor.DipX - other.Width * 96d / Dpi(other.DpiX);
            dipY = anchor.DipY + (other.Y - anchorIn.Y) * 96d / Dpi(anchor.DpiY);
            return true;
        }

        if (other.Y == anchorIn.Y + anchorIn.Height && horizontal > 0)
        {
            dipX = anchor.DipX + (other.X - anchorIn.X) * 96d / Dpi(anchor.DpiX);
            dipY = anchor.DipY + anchor.DipHeight;
            return true;
        }

        if (anchorIn.Y == other.Y + other.Height && horizontal > 0)
        {
            dipX = anchor.DipX + (other.X - anchorIn.X) * 96d / Dpi(anchor.DpiX);
            dipY = anchor.DipY - other.Height * 96d / Dpi(other.DpiY);
            return true;
        }

        return false;
    }

    private static Band BandOf(Input input, double dipX, double dipY)
    {
        var dpiX = Dpi(input.DpiX);
        var dpiY = Dpi(input.DpiY);
        return new Band(input.X, input.Y, input.Width, input.Height, dpiX, dpiY, dipX, dipY, input.Width * 96d / dpiX, input.Height * 96d / dpiY);
    }

    private static double Dpi(double value) => value < 48 ? 96 : value;
}

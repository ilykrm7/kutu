namespace Kutu.Core.Color;

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    public string Rgb => $"rgb({R}, {G}, {B})";

    public string Hsl
    {
        get
        {
            var (h, s, l) = ToHsl();
            return $"hsl({h:0}, {s:0}%, {l:0}%)";
        }
    }

    public (double H, double S, double L) ToHsl()
    {
        double r = R / 255d;
        double g = G / 255d;
        double b = B / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2d;
        if (Math.Abs(max - min) < 0.00001)
            return (0, 0, l * 100d);

        double d = max - min;
        double s = l > 0.5 ? d / (2d - max - min) : d / (max + min);
        double h;
        if (Math.Abs(max - r) < 0.00001)
            h = (g - b) / d + (g < b ? 6 : 0);
        else if (Math.Abs(max - g) < 0.00001)
            h = (b - r) / d + 2;
        else
            h = (r - g) / d + 4;

        return (h * 60d, s * 100d, l * 100d);
    }

    public static bool TryParseHex(string? text, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => $"{c}{c}"));

        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var value))
            return false;

        color = new RgbColor((byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
        return true;
    }
}

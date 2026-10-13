using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kutu.Tools.Screenshot;

public enum ShotTool
{
    Arrow,
    Rectangle,
    Pen,
    Text,
    Blur,
    Highlight
}

public abstract class Annotation
{
    public Color Color { get; set; } = Colors.White;

    public double Thickness { get; set; } = 4;

    public abstract void Draw(DrawingContext dc, BitmapSource source);
}

public sealed class ArrowAnnotation : Annotation
{
    public Point Start { get; set; }

    public Point End { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        var pen = Pen();
        dc.DrawLine(pen, Start, End);
        var angle = Math.Atan2(End.Y - Start.Y, End.X - Start.X);
        var len = 14 + Thickness;
        var p1 = new Point(End.X - (len * Math.Cos(angle - 0.45)), End.Y - (len * Math.Sin(angle - 0.45)));
        var p2 = new Point(End.X - (len * Math.Cos(angle + 0.45)), End.Y - (len * Math.Sin(angle + 0.45)));
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(End, true, true);
            ctx.LineTo(p1, true, true);
            ctx.LineTo(p2, true, true);
        }

        geometry.Freeze();
        dc.DrawGeometry(Brush(), null, geometry);
    }

    private Pen Pen()
    {
        var pen = new Pen(Brush(), Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private SolidColorBrush Brush()
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        return brush;
    }
}

public sealed class RectAnnotation : Annotation
{
    public Rect Bounds { get; set; }

    public bool Filled { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        SolidColorBrush? fill = null;
        if (Filled)
        {
            fill = new SolidColorBrush(Color.FromArgb(90, Color.R, Color.G, Color.B));
            fill.Freeze();
        }

        var pen = new Pen(new SolidColorBrush(Color), Filled ? 0 : Thickness);
        if (!Filled)
            pen.Freeze();
        dc.DrawRectangle(fill, Filled ? null : pen, Bounds);
    }
}

public sealed class PenAnnotation : Annotation
{
    public PointCollection Points { get; } = [];

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        if (Points.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false, false);
            ctx.PolyLineTo(Points.Skip(1).ToList(), true, true);
        }

        geometry.Freeze();
        var pen = new Pen(new SolidColorBrush(Color), Thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }
}

public sealed class TextAnnotation : Annotation
{
    public Point Origin { get; set; }

    public string Text { get; set; } = "";

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        if (string.IsNullOrEmpty(Text))
            return;

        var formatted = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            Math.Max(18, Thickness * 6),
            new SolidColorBrush(Color),
            1);
        dc.DrawText(formatted, Origin);
    }
}

public sealed class BlurAnnotation : Annotation
{
    public Rect Bounds { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        var rect = Rect.Intersect(Bounds, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
        if (rect.IsEmpty || rect.Width < 2 || rect.Height < 2)
            return;

        var x = (int)Math.Floor(rect.X);
        var y = (int)Math.Floor(rect.Y);
        var w = Math.Max(1, (int)Math.Floor(rect.Width));
        var h = Math.Max(1, (int)Math.Floor(rect.Height));
        if (x + w > source.PixelWidth)
            w = source.PixelWidth - x;
        if (y + h > source.PixelHeight)
            h = source.PixelHeight - y;

        var stride = w * 4;
        var pixels = new byte[stride * h];
        source.CopyPixels(new Int32Rect(x, y, w, h), pixels, stride, 0);
        const int block = 12;
        for (var by = 0; by < h; by += block)
        {
            for (var bx = 0; bx < w; bx += block)
            {
                var i = ((by * w) + bx) * 4;
                var color = Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
                var bw = Math.Min(block, w - bx);
                var bh = Math.Min(block, h - by);
                dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(x + bx, y + by, bw, bh));
            }
        }
    }
}

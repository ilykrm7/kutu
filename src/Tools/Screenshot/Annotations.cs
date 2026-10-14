using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kutu.Tools.Screenshot;

public enum ShotTool
{
    Arrow,
    Line,
    Rectangle,
    Ellipse,
    Pen,
    Text,
    Blur,
    Highlight
}

public abstract class Annotation
{
    public Color Color { get; set; } = Color.FromRgb(0xE5, 0x39, 0x35);

    public double Thickness { get; set; } = 4;

    public abstract void Draw(DrawingContext dc, BitmapSource source);

    protected static Pen Stroke(Color color, double thickness, bool roundJoin)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, Math.Max(1, thickness))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = roundJoin ? PenLineJoin.Round : PenLineJoin.Miter,
            MiterLimit = 2
        };
        pen.Freeze();
        return pen;
    }

    protected static void Clip(DrawingContext dc, BitmapSource source, Action draw)
    {
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, source.PixelWidth, source.PixelHeight)));
        draw();
        dc.Pop();
    }

    protected static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    protected static Rect Inset(Rect bounds, double amount)
    {
        if (amount <= 0 || bounds.Width <= amount * 2 || bounds.Height <= amount * 2)
            return bounds;
        bounds.Inflate(-amount, -amount);
        return bounds;
    }
}

public sealed class ArrowAnnotation : Annotation
{
    public Point Start { get; set; }

    public Point End { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        var span = Distance(Start, End);
        if (span < 1)
            return;

        Clip(dc, source, () =>
        {
            var pen = Stroke(Color, Thickness, roundJoin: true);
            dc.DrawLine(pen, Start, End);
            var angle = Math.Atan2(End.Y - Start.Y, End.X - Start.X);
            var len = Math.Min(14 + Thickness, span);
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
            var brush = new SolidColorBrush(Color);
            brush.Freeze();
            dc.DrawGeometry(brush, null, geometry);
        });
    }
}

public sealed class LineAnnotation : Annotation
{
    public Point Start { get; set; }

    public Point End { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        if (Distance(Start, End) < 1)
            return;
        Clip(dc, source, () => dc.DrawLine(Stroke(Color, Thickness, roundJoin: true), Start, End));
    }
}

public sealed class RectAnnotation : Annotation
{
    public Rect Bounds { get; set; }

    public bool Filled { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        if (Bounds.Width < 1 && Bounds.Height < 1)
            return;

        Clip(dc, source, () =>
        {
            if (Filled)
            {
                var fillColor = Color.A == 255 ? Color.FromArgb(96, Color.R, Color.G, Color.B) : Color;
                var fill = new SolidColorBrush(fillColor);
                fill.Freeze();
                dc.DrawRectangle(fill, null, Bounds);
                return;
            }

            var box = Inset(Bounds, Thickness / 2);
            var guidelines = new GuidelineSet([box.Left, box.Right], [box.Top, box.Bottom]);
            dc.PushGuidelineSet(guidelines);
            dc.DrawRectangle(null, Stroke(Color, Thickness, roundJoin: false), box);
            dc.Pop();
        });
    }
}

public sealed class EllipseAnnotation : Annotation
{
    public Rect Bounds { get; set; }

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        if (Bounds.Width < 1 && Bounds.Height < 1)
            return;

        Clip(dc, source, () =>
        {
            var box = Inset(Bounds, Thickness / 2);
            var center = new Point(box.X + (box.Width / 2), box.Y + (box.Height / 2));
            dc.DrawEllipse(null, Stroke(Color, Thickness, roundJoin: true), center, box.Width / 2, box.Height / 2);
        });
    }
}

public sealed class PenAnnotation : Annotation
{
    public PointCollection Points { get; } = [];

    public override void Draw(DrawingContext dc, BitmapSource source)
    {
        if (Points.Count < 2)
            return;

        Clip(dc, source, () =>
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(Points[0], false, false);
                ctx.PolyLineTo(Points.Skip(1).ToList(), true, true);
            }

            geometry.Freeze();
            dc.DrawGeometry(null, Stroke(Color, Thickness, roundJoin: true), geometry);
        });
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

        var size = Thickness switch
        {
            <= 2.5 => 16d,
            <= 5 => 22d,
            _ => 32d
        };
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var formatted = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            size,
            brush,
            1);
        Clip(dc, source, () => dc.DrawText(formatted, Origin));
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
        if (w < 1 || h < 1)
            return;

        var stride = w * 4;
        var pixels = new byte[stride * h];
        source.CopyPixels(new Int32Rect(x, y, w, h), pixels, stride, 0);
        const int block = 12;
        Clip(dc, source, () =>
        {
            for (var by = 0; by < h; by += block)
            {
                for (var bx = 0; bx < w; bx += block)
                {
                    var i = ((by * w) + bx) * 4;
                    var color = Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
                    var bw = Math.Min(block, w - bx);
                    var bh = Math.Min(block, h - by);
                    var fill = new SolidColorBrush(color);
                    fill.Freeze();
                    dc.DrawRectangle(fill, null, new Rect(x + bx, y + by, bw, bh));
                }
            }
        });
    }
}

namespace Kutu.Core.Imaging;

public static class Matte
{
    public readonly record struct Rgb(byte B, byte G, byte R);

    public static byte[] ClearBackground(byte[] bgra, int width, int height, int tolerance, bool holes, int feather)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (bgra is null)
            throw new ArgumentNullException(nameof(bgra));
        if (bgra.Length != width * height * 4)
            throw new ArgumentException("BGRA buffer length does not match the image.", nameof(bgra));

        if (tolerance < 0)
            tolerance = 0;
        if (tolerance > 255)
            tolerance = 255;
        if (feather < 0)
            feather = 0;
        if (feather > 8)
            feather = 8;

        var count = width * height;
        var remove = new bool[count];
        var reference = CornerColor(bgra, width, height);
        if (holes)
        {
            for (var i = 0; i < count; i++)
            {
                if (Near(bgra, i * 4, reference, tolerance))
                    remove[i] = true;
            }
        }
        else
        {
            Flood(bgra, width, height, reference, tolerance, remove);
        }

        var output = (byte[])bgra.Clone();
        for (var i = 0; i < count; i++)
        {
            if (remove[i])
                output[i * 4 + 3] = 0;
        }

        if (feather > 0)
            Feather(output, remove, width, height, feather);

        return output;
    }

    public static Rgb CornerColor(byte[] bgra, int width, int height)
    {
        long b = 0, g = 0, r = 0;
        var corners = new[]
        {
            0,
            width - 1,
            (height - 1) * width,
            (height - 1) * width + (width - 1)
        };
        foreach (var index in corners)
        {
            var offset = index * 4;
            b += bgra[offset];
            g += bgra[offset + 1];
            r += bgra[offset + 2];
        }

        return new Rgb((byte)(b / 4), (byte)(g / 4), (byte)(r / 4));
    }

    private static void Flood(byte[] bgra, int width, int height, Rgb reference, int tolerance, bool[] remove)
    {
        var queue = new Queue<int>();
        void TryAdd(int index)
        {
            if (remove[index] || !Near(bgra, index * 4, reference, tolerance))
                return;
            remove[index] = true;
            queue.Enqueue(index);
        }

        for (var x = 0; x < width; x++)
        {
            TryAdd(x);
            TryAdd((height - 1) * width + x);
        }

        for (var y = 1; y < height - 1; y++)
        {
            TryAdd(y * width);
            TryAdd(y * width + width - 1);
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;
            if (x > 0)
                TryAdd(index - 1);
            if (x + 1 < width)
                TryAdd(index + 1);
            if (y > 0)
                TryAdd(index - width);
            if (y + 1 < height)
                TryAdd(index + width);
        }
    }

    private static void Feather(byte[] output, bool[] remove, int width, int height, int feather)
    {
        var count = width * height;
        var alpha = new byte[count];
        for (var i = 0; i < count; i++)
            alpha[i] = output[i * 4 + 3];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (remove[index])
                    continue;

                var best = feather + 1;
                for (var dy = -feather; dy <= feather; dy++)
                {
                    var ny = y + dy;
                    if ((uint)ny >= (uint)height)
                        continue;
                    for (var dx = -feather; dx <= feather; dx++)
                    {
                        var nx = x + dx;
                        if ((uint)nx >= (uint)width)
                            continue;
                        var dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        if (dist == 0 || dist >= best)
                            continue;
                        if (remove[ny * width + nx])
                            best = dist;
                    }
                }

                if (best <= feather)
                    alpha[index] = (byte)(output[index * 4 + 3] * best / (feather + 1));
            }
        }

        for (var i = 0; i < count; i++)
            output[i * 4 + 3] = alpha[i];
    }

    private static bool Near(byte[] bgra, int offset, Rgb color, int tolerance) =>
        Math.Abs(bgra[offset] - color.B) <= tolerance
        && Math.Abs(bgra[offset + 1] - color.G) <= tolerance
        && Math.Abs(bgra[offset + 2] - color.R) <= tolerance;
}

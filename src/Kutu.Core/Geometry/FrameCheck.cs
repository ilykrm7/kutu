namespace Kutu.Core.Geometry;

public static class FrameCheck
{
    public static bool IsUniformBlack(byte[]? bgra, int stride, int width, int height)
    {
        if (bgra is null || width <= 0 || height <= 0 || stride < width * 4 || bgra.Length < stride * height)
            return true;

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var index = row + (x * 4);
                if (bgra[index] > 2 || bgra[index + 1] > 2 || bgra[index + 2] > 2)
                    return false;
            }
        }

        return true;
    }
}

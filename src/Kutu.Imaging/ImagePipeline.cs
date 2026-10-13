using ImageMagick;

namespace Kutu.Imaging;

public static class ImagePipeline
{
    public static Task ConvertAsync(
        string inputPath,
        string outputPath,
        KutuImageFormat format,
        int quality,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(inputPath);
        Save(image, outputPath, format, quality);
        return Task.CompletedTask;
    }

    public static Task ResizeAndSaveAsync(
        string inputPath,
        string outputPath,
        FitMode mode,
        int value,
        KutuImageFormat format,
        int quality,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(inputPath);
        Fit(image, mode, value);
        Save(image, outputPath, format, quality);
        return Task.CompletedTask;
    }

    public static void Fit(MagickImage image, FitMode mode, int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));

        switch (mode)
        {
            case FitMode.Percent:
                image.Resize(new Percentage(value));
                break;
            case FitMode.Width:
                image.Resize(new MagickGeometry((uint)value, 0));
                break;
            case FitMode.Height:
                image.Resize(new MagickGeometry(0, (uint)value));
                break;
            default:
                if (image.Width >= image.Height)
                    image.Resize(new MagickGeometry((uint)value, 0));
                else
                    image.Resize(new MagickGeometry(0, (uint)value));
                break;
        }
    }

    public static void Save(MagickImage image, string outputPath, KutuImageFormat format, int quality)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        image.Quality = (uint)Math.Clamp(quality, 1, 100);
        if (format == KutuImageFormat.Ico)
        {
            SaveIco(image, outputPath);
            return;
        }

        image.Format = format switch
        {
            KutuImageFormat.Jpeg => MagickFormat.Jpeg,
            KutuImageFormat.Webp => MagickFormat.WebP,
            KutuImageFormat.Bmp => MagickFormat.Bmp,
            _ => MagickFormat.Png
        };
        image.Write(outputPath);
    }

    public static void SaveIco(MagickImage image, string path, int[]? sizes = null)
    {
        sizes ??= [16, 32, 48, 256];
        var pngs = new List<byte[]>(sizes.Length);
        foreach (var size in sizes)
        {
            using var clone = image.Clone();
            clone.Resize(new MagickGeometry((uint)size, (uint)size) { IgnoreAspectRatio = true });
            clone.Format = MagickFormat.Png;
            pngs.Add(clone.ToByteArray());
        }

        using var output = File.Create(path);
        using var writer = new BinaryWriter(output);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)pngs.Count);
        var offset = 6 + (16 * pngs.Count);
        for (var i = 0; i < pngs.Count; i++)
        {
            var size = sizes[i];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(pngs[i].Length);
            writer.Write(offset);
            offset += pngs[i].Length;
        }

        foreach (var png in pngs)
            writer.Write(png);
    }
}

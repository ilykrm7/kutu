namespace Kutu.Imaging;

public enum KutuImageFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp,
    Ico
}

public enum FitMode
{
    LongEdge,
    Width,
    Height,
    Percent
}

public static class ImageFormats
{
    public static readonly string[] ReadableExtensions =
    [
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".heic", ".heif"
    ];

    public static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path);
        return ReadableExtensions.Any(item => item.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsHeic(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".heic", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase);
    }

    public static string Extension(KutuImageFormat format) => format switch
    {
        KutuImageFormat.Png => ".png",
        KutuImageFormat.Jpeg => ".jpg",
        KutuImageFormat.Webp => ".webp",
        KutuImageFormat.Bmp => ".bmp",
        KutuImageFormat.Ico => ".ico",
        _ => ".png"
    };

    public static string OutputPath(string inputPath, KutuImageFormat format)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, name + ".kutu" + Extension(format));
    }
}

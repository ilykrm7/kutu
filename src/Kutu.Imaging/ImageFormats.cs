namespace Kutu.Imaging;

public enum KutuImageFormat
{
    Png,
    Jpeg,
    Webp,
    Bmp,
    Ico,
    Pdf
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
        KutuImageFormat.Pdf => ".pdf",
        _ => ".png"
    };

    public static string OutputPath(string inputPath, KutuImageFormat format)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? "";
        var name = CleanStem(inputPath);
        var extension = Extension(format);
        var candidate = Path.Combine(directory, name + extension);
        if (!Collides(candidate, inputPath))
            return candidate;

        for (var index = 2; index < 10000; index++)
        {
            candidate = Path.Combine(directory, name + "-" + index + extension);
            if (!Collides(candidate, inputPath))
                return candidate;
        }

        return Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N") + extension);
    }

    private static string CleanStem(string inputPath)
    {
        var name = Path.GetFileNameWithoutExtension(inputPath);
        while (name.EndsWith(".kutu", StringComparison.OrdinalIgnoreCase))
            name = name[..^5];
        return name.Length == 0 ? "kutu" : name;
    }

    private static bool Collides(string candidate, string inputPath)
    {
        var full = Path.GetFullPath(candidate);
        if (full.Equals(Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
            return true;
        return File.Exists(full);
    }
}

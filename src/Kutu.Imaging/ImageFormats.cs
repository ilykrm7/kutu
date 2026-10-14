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
        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrEmpty(directory))
            directory = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        var name = Path.GetFileNameWithoutExtension(inputPath);
        if (name.Length == 0)
            name = "kutu";
        var extension = Extension(format);
        var inputFull = Path.GetFullPath(inputPath);
        for (var index = 1; index < 10000; index++)
        {
            var fileName = index == 1 ? name + extension : name + "-" + index + extension;
            var candidate = Path.Combine(directory, fileName);
            if (PathsEqual(candidate, inputFull))
                continue;
            try
            {
                using (new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                }

                return candidate;
            }
            catch (IOException)
            {
                // Another selected file already claimed this name.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N") + extension);
    }

    private static bool PathsEqual(string candidate, string inputFull)
    {
        try
        {
            return Path.GetFullPath(candidate).Equals(inputFull, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

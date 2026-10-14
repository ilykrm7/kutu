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
        try
        {
            WicCodec.Convert(inputPath, outputPath, format, quality, null, 0);
            return Task.CompletedTask;
        }
        catch
        {
            DiscardOutput(outputPath, inputPath);
            throw;
        }
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
        try
        {
            WicCodec.Convert(inputPath, outputPath, format, quality, mode, value);
            return Task.CompletedTask;
        }
        catch
        {
            DiscardOutput(outputPath, inputPath);
            throw;
        }
    }

    private static void DiscardOutput(string outputPath, string inputPath)
    {
        try
        {
            if (Path.GetFullPath(outputPath).Equals(Path.GetFullPath(inputPath), StringComparison.OrdinalIgnoreCase))
                return;
            File.Delete(outputPath);
        }
        catch
        {
            // The failed output may already be gone.
        }
    }
}

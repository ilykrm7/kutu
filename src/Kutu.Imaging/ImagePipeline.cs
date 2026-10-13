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
        WicCodec.Convert(inputPath, outputPath, format, quality, null, 0);
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
        WicCodec.Convert(inputPath, outputPath, format, quality, mode, value);
        return Task.CompletedTask;
    }
}

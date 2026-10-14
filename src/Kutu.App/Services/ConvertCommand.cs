using Kutu.Core.Diagnostics;
using Kutu.Imaging;
using Kutu.Sdk.Localization;

namespace Kutu.App.Services;

internal static class ConvertCommand
{
    public static string Execute(string formatName, IReadOnlyList<string> files, int quality)
    {
        var format = formatName.ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => KutuImageFormat.Jpeg,
            "png" => KutuImageFormat.Png,
            "webp" => KutuImageFormat.Webp,
            "ico" => KutuImageFormat.Ico,
            "pdf" => KutuImageFormat.Pdf,
            "bmp" => KutuImageFormat.Bmp,
            _ => (KutuImageFormat?)null
        };
        if (format is null)
            return L.Get("Image_Failed");
        if (files.Count == 0)
            return L.Get("Image_None");

        var done = 0;
        var failed = new List<string>();
        var heif = false;
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                failed.Add(Path.GetFileName(file));
                AppLog.Write(file + Environment.NewLine + "missing");
                continue;
            }

            try
            {
                ImagePipeline.ConvertAsync(file, ImageFormats.OutputPath(file, format.Value), format.Value, quality)
                    .GetAwaiter()
                    .GetResult();
                done++;
            }
            catch (HeifCodecMissingException ex)
            {
                heif = true;
                failed.Add(Path.GetFileName(file));
                AppLog.Write(file + Environment.NewLine + ex);
            }
            catch (Exception ex)
            {
                failed.Add(Path.GetFileName(file));
                AppLog.Write(file + Environment.NewLine + ex);
            }
        }

        if (failed.Count == 0)
            return string.Format(L.Get("Image_Done"), done);

        var text = string.Format(L.Get("Image_FailedFile"), string.Join(", ", failed));
        return heif ? L.Get("Image_HeifMissing") + " " + text : text;
    }
}

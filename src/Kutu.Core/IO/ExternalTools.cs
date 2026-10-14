using Kutu.Core.Settings;

namespace Kutu.Core.IO;

public static class ExternalTools
{
    public const string YtDlpRepo = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";

    public const string FfmpegRepo = "https://api.github.com/repos/yt-dlp/FFmpeg-Builds/releases/latest";

    public const string YtDlpFile = "yt-dlp.exe";

    public const string FfmpegZip = "ffmpeg-master-latest-win64-gpl.zip";

    public const string FfmpegSums = "checksums.sha256";

    public const string YtDlpSums = "SHA2-256SUMS";

    public static string DirectoryPath => Path.Combine(SettingsService.AppDirectory, "tools");

    public static string YtDlpPath => Path.Combine(DirectoryPath, YtDlpFile);

    public static string FfmpegPath => Path.Combine(DirectoryPath, "ffmpeg.exe");

    public static bool HasYtDlp => File.Exists(YtDlpPath);

    public static bool HasFfmpeg => File.Exists(FfmpegPath);
}

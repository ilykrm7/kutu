using System.IO.Compression;
using System.Net.Http;
using Kutu.Core.Network;
using Kutu.Core.Updates;

namespace Kutu.Core.IO;

public readonly record struct FetchProgress(string Label, double Fraction, long Received = 0, long Total = 0, double BytesPerSecond = 0);

public static class ExternalToolFetch
{
    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        TlsPolicy.Enable();
        return new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
    }

    static ExternalToolFetch()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Kutu/1.1");
        Http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static Task DownloadFileAsync(string url, string path, IProgress<FetchProgress>? progress, CancellationToken cancellationToken) =>
        ToFileAsync(url, path, progress is null ? null : progress.Report, "Kutu", cancellationToken);

    public static async Task<long?> ProbeLengthAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await Http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength is > 0)
                return response.Content.Headers.ContentLength;
        }
        catch
        {
            // Some hosts reject HEAD. The range request below reads the length.
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.Content.Headers.ContentRange?.Length is > 0)
                return response.Content.Headers.ContentRange.Length;
            if (response.Content.Headers.ContentLength is > 0)
                return response.Content.Headers.ContentLength;
        }
        catch
        {
            // The button stays unlabeled when the size cannot be read.
        }

        return null;
    }

    public static async Task<long?> AssetLengthAsync(string releaseUrl, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var release = await LatestAsync(releaseUrl, cancellationToken);
            var file = ReleaseVersion.Find(release, fileName);
            if (file is null || string.IsNullOrWhiteSpace(file.Url))
                return null;
            return await ProbeLengthAsync(file.Url, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> GetTextAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static Task EnsureAsync(bool ytDlp, bool ffmpeg, IProgress<FetchProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ExternalTools.DirectoryPath);
        return EnsureCoreAsync(ytDlp, ffmpeg, progress, cancellationToken);
    }

    private static async Task EnsureCoreAsync(bool ytDlp, bool ffmpeg, IProgress<FetchProgress>? progress, CancellationToken cancellationToken)
    {
        if (ytDlp)
            await FetchBinaryAsync(ExternalTools.YtDlpRepo, ExternalTools.YtDlpFile, ExternalTools.YtDlpSums, ExternalTools.YtDlpPath, progress, cancellationToken);
        if (ffmpeg)
            await FetchFfmpegAsync(progress, cancellationToken);
    }

    private static async Task FetchBinaryAsync(string releaseUrl, string fileName, string sumsName, string destination, IProgress<FetchProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new FetchProgress(fileName, 0));
        var release = await LatestAsync(releaseUrl, cancellationToken);
        var file = ReleaseVersion.Find(release, fileName) ?? throw new InvalidOperationException(fileName);
        var sums = ReleaseVersion.Find(release, sumsName) ?? throw new InvalidOperationException(sumsName);
        var sumsText = await Http.GetStringAsync(sums.Url);
        var temp = destination + ".download";
        try
        {
            await ToFileAsync(file.Url, temp, snapshot => progress?.Report(snapshot with { Label = fileName }), fileName, cancellationToken);
            if (!Sha256Check.Matches(temp, sumsText, fileName))
            {
                TryDelete(temp);
                throw new InvalidOperationException("hash");
            }

            Replace(temp, destination);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static async Task FetchFfmpegAsync(IProgress<FetchProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new FetchProgress(ExternalTools.FfmpegZip, 0));
        var release = await LatestAsync(ExternalTools.FfmpegRepo, cancellationToken);
        var zip = ReleaseVersion.Find(release, ExternalTools.FfmpegZip) ?? throw new InvalidOperationException(ExternalTools.FfmpegZip);
        var sums = ReleaseVersion.Find(release, ExternalTools.FfmpegSums) ?? throw new InvalidOperationException(ExternalTools.FfmpegSums);
        var sumsText = await Http.GetStringAsync(sums.Url);
        var temp = Path.Combine(Path.GetTempPath(), "kutu-ffmpeg-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await ToFileAsync(zip.Url, temp, snapshot => progress?.Report(snapshot with { Label = "ffmpeg" }), "ffmpeg", cancellationToken);
            if (!Sha256Check.Matches(temp, sumsText, ExternalTools.FfmpegZip))
            {
                TryDelete(temp);
                throw new InvalidOperationException("hash");
            }

            var extracted = Path.Combine(Path.GetTempPath(), "kutu-ffmpeg-" + Guid.NewGuid().ToString("N") + ".exe");
            using (var archive = ZipFile.OpenRead(temp))
            {
                var entry = archive.Entries.FirstOrDefault(item => item.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("ffmpeg.exe");
                entry.ExtractToFile(extracted, overwrite: true);
            }

            Replace(extracted, ExternalTools.FfmpegPath);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static async Task<IReadOnlyList<ReleaseAsset>> LatestAsync(string url, CancellationToken cancellationToken)
    {
        var json = await Http.GetStringAsync(url);
        return ReleaseVersion.ParseLatest(json)?.Assets ?? throw new InvalidOperationException("release");
    }

    private static async Task ToFileAsync(string url, string path, Action<FetchProgress>? progress, string label, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        using var input = await response.Content.ReadAsStreamAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = File.Create(path);
        var buffer = new byte[1024 * 128];
        var meter = new TransferMeter();
        long done = 0;
        progress?.Invoke(new FetchProgress(label, 0, 0, total, 0));
        while (true)
        {
            var read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (read == 0)
                break;
            await output.WriteAsync(buffer, 0, read, cancellationToken);
            done += read;
            var fraction = total > 0 ? done / (double)total : 0;
            progress?.Invoke(new FetchProgress(label, fraction, done, total, meter.Observe(done, DateTime.UtcNow)));
        }
    }

    private static void Replace(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
            File.Replace(source, destination, null);
        else
            File.Move(source, destination);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A leftover download is removed on the next attempt.
        }
    }
}

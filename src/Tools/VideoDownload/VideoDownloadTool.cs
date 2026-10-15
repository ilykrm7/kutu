using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Kutu.Core.IO;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;

namespace Kutu.Tools.VideoDownload;

public sealed class VideoDownloadTool : IKutuTool
{
    public string Id => "video";

    public string TitleKey => "Tool_Video_Title";

    public string DescriptionKey => "Tool_Video_Desc";

    public string Symbol => "ArrowDownload24";

    public IReadOnlyList<string> Aliases { get; } = ["video", "yt", "youtube", "indir", "mp3", "mp4", "yt-dlp"];

    public int Order => 130;

    public UIElement CreateView(IKutuHost host) => new VideoDownloadView(host);
}

public sealed class VideoDownloadView : UserControl
{
    private static readonly Regex Percent = new(@"(\d+(?:[\.,]\d+)?)%", RegexOptions.Compiled);
    private readonly IKutuHost _host;
    private readonly TextBox _url;
    private readonly TextBox _folder;
    private readonly ComboBox _format;
    private readonly ComboBox _quality;
    private readonly TextBlock _status;
    private CancellationTokenSource? _fetch;
    private Process? _running;

    public VideoDownloadView(IKutuHost host)
    {
        _host = host;
        _url = Chrome.Input("https://");
        _folder = Chrome.Input(L.Get("Video_Folder"));
        _folder.Text = string.IsNullOrWhiteSpace(host.Settings.VideoOutputFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : host.Settings.VideoOutputFolder;
        _format = Choice("MP4", "MP3");
        _quality = Choice("best", "1080", "720", "480");
        _status = Chrome.Hint(ReadyText());
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(Chrome.Primary(L.Get("Action_Download"), SymbolRegular.ArrowDownload24, async (_, _) => await FetchToolsAsync()));
        row.Children.Add(Chrome.Primary(L.Get("Video_Start"), SymbolRegular.Play24, async (_, _) => await StartAsync()));
        row.Children.Add(Chrome.Secondary(L.Get("Action_Cancel"), SymbolRegular.Dismiss24, (_, _) => Cancel()));
        var options = new StackPanel { Orientation = Orientation.Horizontal };
        options.Children.Add(_format);
        options.Children.Add(_quality);
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_DownloadHint")));
        stack.Children.Add(Chrome.Label(L.Get("Video_Url")));
        stack.Children.Add(_url);
        stack.Children.Add(Chrome.Label(L.Get("Video_Folder")));
        stack.Children.Add(_folder);
        stack.Children.Add(options);
        stack.Children.Add(row);
        stack.Children.Add(_status);
        Content = stack;
    }

    private async Task FetchToolsAsync()
    {
        _fetch?.Cancel();
        var cancel = new CancellationTokenSource();
        _fetch = cancel;
        try
        {
            var progress = new Progress<FetchProgress>(item => _status.Text = item.Label + " " + (int)(item.Fraction * 100) + "%");
            await ExternalToolFetch.EnsureAsync(ytDlp: true, ffmpeg: true, progress, cancel.Token);
            _status.Text = L.Get("Tool_Ready");
        }
        catch (OperationCanceledException)
        {
            _status.Text = L.Get("Video_Cancel");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message == "hash" ? L.Get("Tool_HashFail") : ex.Message;
        }
    }

    private async Task StartAsync()
    {
        if (!ExternalTools.HasYtDlp)
        {
            _status.Text = L.Get("Video_NeedTool");
            return;
        }

        var url = _url.Text.Trim();
        if (url.Length == 0)
            return;
        var mp3 = (_format.SelectedItem as string) == "MP3";
        if (mp3 && !ExternalTools.HasFfmpeg)
        {
            _status.Text = L.Get("Video_NeedFfmpeg");
            return;
        }

        var folder = string.IsNullOrWhiteSpace(_folder.Text)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : _folder.Text.Trim();
        Directory.CreateDirectory(folder);
        _host.Settings.VideoOutputFolder = folder;
        await _host.SaveSettingsAsync();
        var quality = _quality.SelectedItem as string ?? "best";
        var args = new List<string>
        {
            "--newline", "--no-playlist", "--no-mtime",
            "-o", Path.Combine(folder, "%(title)s.%(ext)s"),
            url
        };
        if (ExternalTools.HasFfmpeg)
        {
            args.Insert(0, Path.GetDirectoryName(ExternalTools.FfmpegPath)!);
            args.Insert(0, "--ffmpeg-location");
        }

        if (mp3)
        {
            args.Add("-x");
            args.Add("--audio-format");
            args.Add("mp3");
            args.Add("-f");
            args.Add("ba/b");
        }
        else
        {
            args.Add("-f");
            args.Add(Format(quality, ExternalTools.HasFfmpeg));
            if (ExternalTools.HasFfmpeg)
            {
                args.Add("--merge-output-format");
                args.Add("mp4");
            }
        }

        Cancel();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ExternalTools.YtDlpPath,
                Arguments = WinProcess.Join(args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, e) => Note(e.Data);
        process.ErrorDataReceived += (_, e) => Note(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _running = process;
        _status.Text = L.Get("Video_Working");
        await Task.Run(() => process.WaitForExit());
        if (ReferenceEquals(_running, process))
            _running = null;
        if (_status.Text == L.Get("Video_Working"))
            _status.Text = process.ExitCode == 0 ? L.Get("Video_Done") : L.Get("Video_Fail");
    }

    private void Note(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        var match = Percent.Match(line);
        var text = match.Success ? L.Get("Video_Working") + " " + match.Groups[1].Value + "%" : line.Trim();
        if (text.Length > 180)
            text = text[..180];
        Dispatcher.BeginInvoke(new Action(() => _status.Text = text));
    }

    private void Cancel()
    {
        _fetch?.Cancel();
        var process = _running;
        _running = null;
        if (process is { HasExited: false })
            WinProcess.KillTree(process.Id);
    }

    private static string Format(string quality, bool ffmpeg)
    {
        if (!ffmpeg)
            return "b[ext=mp4]/b";
        var height = quality switch
        {
            "1080" => "[height<=1080]",
            "720" => "[height<=720]",
            "480" => "[height<=480]",
            _ => ""
        };
        return $"bv*{height}[ext=mp4]+ba[ext=m4a]/b{height}[ext=mp4]/b";
    }

    private static string ReadyText() =>
        ExternalTools.HasYtDlp ? (ExternalTools.HasFfmpeg ? "yt-dlp + ffmpeg" : "yt-dlp") : "";

    private static ComboBox Choice(params string[] items)
    {
        var box = new ComboBox { Width = 120, Height = 36, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var item in items)
            box.Items.Add(item);
        box.SelectedIndex = 0;
        return box;
    }
}

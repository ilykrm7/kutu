using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Kutu.Core.IO;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Kutu.Tools.Screenshot;
using Wpf.Ui.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;

namespace Kutu.Tools.ScreenRecord;

public sealed class ScreenRecordTool : IKutuTool
{
    public string Id => "screen-record";

    public string TitleKey => "Tool_Record_Title";

    public string DescriptionKey => "Tool_Record_Desc";

    public string Symbol => "Record24";

    public IReadOnlyList<string> Aliases { get; } = ["kayıt", "kayit", "record", "gif", "ekran kaydı", "ffmpeg"];

    public int Order => 38;

    public UIElement CreateView(IKutuHost host) => new ScreenRecordView(host);
}

public static class ScreenRecordController
{
    private static Process? _process;
    private static Window? _badge;

    public static bool IsRecording => _process is { HasExited: false };

    public static async Task ToggleAsync(IKutuHost host)
    {
        if (IsRecording)
        {
            Stop();
            return;
        }

        if (!ExternalTools.HasFfmpeg)
        {
            host.Toast(L.Get("Record_Need"));
            host.OpenTool("screen-record");
            return;
        }

        host.HideForCapture();
        RegionPick? pick;
        try
        {
            await Task.Delay(160);
            pick = RegionOverlay.Choose(true, L.Get("Action_FullScreen"));
        }
        finally
        {
            host.RestoreAfterCapture();
        }

        if (pick is null)
            return;
        Start(host, pick, mp4: true, cursor: true);
    }

    public static void Start(IKutuHost host, RegionPick pick, bool mp4, bool cursor)
    {
        Stop();
        var folder = string.IsNullOrWhiteSpace(host.Settings.VideoOutputFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : host.Settings.VideoOutputFolder;
        Directory.CreateDirectory(folder);
        var ext = mp4 ? "mp4" : "gif";
        var path = Path.Combine(folder, "Kutu-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "." + ext);
        var width = Math.Max(2, pick.Width & ~1);
        var height = Math.Max(2, pick.Height & ~1);
        var args = new List<string>
        {
            "-y", "-f", "gdigrab", "-framerate", mp4 ? "30" : "12",
            "-draw_mouse", cursor ? "1" : "0",
            "-offset_x", pick.ScreenX.ToString(),
            "-offset_y", pick.ScreenY.ToString(),
            "-video_size", width + "x" + height,
            "-i", "desktop"
        };
        if (mp4)
        {
            args.AddRange(["-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "ultrafast"]);
        }
        else
        {
            args.Add("-vf");
            args.Add("fps=12,scale=trunc(iw/2)*2:-1:flags=lanczos,split[s0][s1];[s0]palettegen=stats_mode=diff[p];[s1][p]paletteuse");
        }

        args.Add(path);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ExternalTools.FfmpegPath,
                Arguments = WinProcess.Join(args),
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        _process = process;
        ShowBadge();
    }

    public static void Stop()
    {
        var process = _process;
        _process = null;
        HideBadge();
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
            {
                process.StandardInput.WriteLine("q");
                if (!process.WaitForExit(2500))
                    WinProcess.KillTree(process.Id);
            }
        }
        catch
        {
            WinProcess.KillTree(process.Id);
        }
    }

    private static void ShowBadge()
    {
        HideBadge();
        var button = Chrome.Secondary(L.Get("Action_Stop"), SymbolRegular.RecordStop24, (_, _) => Stop());
        var text = new TextBlock { Text = L.Get("Record_Live"), Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(text);
        row.Children.Add(button);
        _badge = new Window
        {
            Width = 280,
            SizeToContent = SizeToContent.Height,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            Left = 24,
            Top = 24,
            Content = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0x18, 0x28)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x5A, 0x5A)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = row
            }
        };
        _badge.Show();
    }

    private static void HideBadge()
    {
        try
        {
            _badge?.Close();
        }
        catch
        {
            // The badge window may already be closed.
        }

        _badge = null;
    }
}

public sealed class ScreenRecordView : UserControl
{
    private readonly IKutuHost _host;
    private readonly ComboBox _format;
    private readonly CheckBox _cursor;
    private readonly TextBlock _status;
    private CancellationTokenSource? _fetch;

    public ScreenRecordView(IKutuHost host)
    {
        _host = host;
        _format = new ComboBox { Width = 120, Height = 36, Margin = new Thickness(0, 0, 8, 0) };
        _format.Items.Add("MP4");
        _format.Items.Add("GIF");
        _format.SelectedIndex = 0;
        _cursor = new CheckBox { Content = L.Get("Record_Cursor"), IsChecked = true, Foreground = Chrome.Text, VerticalAlignment = VerticalAlignment.Center };
        _status = Chrome.Hint(ExternalTools.HasFfmpeg ? L.Get("Tool_Ready") : L.Get("Record_Need"));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_format);
        row.Children.Add(_cursor);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Download"), SymbolRegular.ArrowDownload24, async (_, _) => await FetchAsync()));
        buttons.Children.Add(Chrome.Primary(L.Get("Record_Start"), SymbolRegular.Record24, async (_, _) => await BeginAsync()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Stop"), SymbolRegular.RecordStop24, (_, _) => ScreenRecordController.Stop()));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Tool_DownloadHint")));
        stack.Children.Add(Chrome.Hint(string.Format(L.Get("Dash_Hint"), host.Settings.RecordHotkey)));
        stack.Children.Add(row);
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        Content = stack;
    }

    private async Task FetchAsync()
    {
        _fetch?.Cancel();
        var cancel = new CancellationTokenSource();
        _fetch = cancel;
        try
        {
            var progress = new Progress<FetchProgress>(item => _status.Text = item.Label + " " + (int)(item.Fraction * 100) + "%");
            await ExternalToolFetch.EnsureAsync(false, true, progress, cancel.Token);
            _status.Text = L.Get("Tool_Ready");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message == "hash" ? L.Get("Tool_HashFail") : ex.Message;
        }
    }

    private async Task BeginAsync()
    {
        if (!ExternalTools.HasFfmpeg)
        {
            _status.Text = L.Get("Record_Need");
            return;
        }

        _host.HideForCapture();
        RegionPick? pick;
        try
        {
            await Task.Delay(160);
            pick = RegionOverlay.Choose(true, L.Get("Action_FullScreen"));
        }
        finally
        {
            _host.RestoreAfterCapture();
        }

        if (pick is null)
            return;
        ScreenRecordController.Start(_host, pick, (_format.SelectedItem as string) != "GIF", _cursor.IsChecked == true);
        _status.Text = L.Get("Record_Live");
    }
}

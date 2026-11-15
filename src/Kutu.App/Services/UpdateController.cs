using System.Diagnostics;
using System.Net.Http;
using System.Windows.Threading;
using Kutu.Core;
using Kutu.Core.Diagnostics;
using Kutu.Core.IO;
using Kutu.Core.Settings;
using Kutu.Core.Updates;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;

namespace Kutu.App.Services;

public sealed class UpdateController
{
    private readonly IKutuHost _host;
    private readonly MainWindow _window;
    private readonly Action _releaseInstance;
    private readonly DispatcherTimer _timer;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private RemoteRelease? _release;
    private DateTime _idleSince = DateTime.UtcNow;
    private bool _startup = true;
    private bool _applying;
    private CancellationTokenSource? _applyCancel;

    public UpdateController(IKutuHost host, MainWindow window, Action releaseInstance)
    {
        _host = host;
        _window = window;
        _releaseInstance = releaseInstance;
        Current = this;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Kutu/1.1");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(12) };
        _timer.Tick += (_, _) => _ = CheckAsync(manual: false);
        _window.PreviewMouseDown += (_, _) => _idleSince = DateTime.UtcNow;
        _window.PreviewKeyDown += (_, _) => _idleSince = DateTime.UtcNow;
    }

    public static UpdateController? Current { get; private set; }

    public string? Available { get; private set; }

    public event Action? Changed;

    public void Start()
    {
        if (!_host.Settings.CheckForUpdates && !_host.Settings.AutoUpdate)
            return;
        _ = CheckAsync(manual: false);
        _timer.Start();
    }

    public void EnsureWatching()
    {
        if (_host.Settings.CheckForUpdates || _host.Settings.AutoUpdate)
            _timer.Start();
    }

    public void CheckNow() => _ = CheckAsync(manual: true);

    public void DismissBar()
    {
        if (_applying)
        {
            _applyCancel?.Cancel();
            return;
        }

        if (string.IsNullOrWhiteSpace(Available))
            return;
        _host.Settings.DismissedUpdateVersion = Available;
        _ = _host.SaveSettingsAsync();
        _window.HideUpdateBar();
    }

    public void Apply() => _ = ApplyAsync();

    private async Task CheckAsync(bool manual)
    {
        if (!manual && !_host.Settings.CheckForUpdates && !_host.Settings.AutoUpdate)
            return;
        try
        {
            var json = await _http.GetStringAsync("https://api.github.com/repos/ilykrm7/kutu/releases/latest");
            var release = ReleaseVersion.ParseLatest(json);
            var local = typeof(UpdateController).Assembly.GetName().Version ?? new Version(1, 1, 0);
            if (release is null || !ReleaseVersion.IsNewer(release.Version, local))
            {
                _release = null;
                Available = null;
                _window.HideUpdateBar();
                Changed?.Invoke();
                if (manual)
                    _host.Toast(L.Get("Update_None"));
                _startup = false;
                return;
            }

            _release = release;
            Available = release.Version.Trim().TrimStart('v', 'V');
            var hidden = !_window.IsVisible;
            var dismissed = string.Equals(_host.Settings.DismissedUpdateVersion, Available, StringComparison.OrdinalIgnoreCase);
            if (!dismissed)
                _window.ShowUpdateBar(Available);
            if (hidden)
                _host.NotifyTray(string.Format(L.Get("Update_Available"), Available));
            Changed?.Invoke();
            var nextLaunch = _startup;
            _startup = false;
            if (_host.Settings.AutoUpdate && (nextLaunch || hidden || DateTime.UtcNow - _idleSince > TimeSpan.FromMinutes(3)))
                await ApplyAsync();
        }
        catch
        {
            _startup = false;
            if (manual)
                _host.Toast(L.Get("Update_Offline"));
        }
    }

    private async Task ApplyAsync()
    {
        if (_applying || _release is null || string.IsNullOrWhiteSpace(Available))
            return;
        var exe = ReleaseVersion.Find(_release.Assets, "Kutu.exe");
        var hash = ReleaseVersion.Find(_release.Assets, "Kutu.exe.sha256");
        var current = HostProcess.Executable;
        if (exe is null || hash is null || string.IsNullOrWhiteSpace(current))
        {
            AppLog.Write("update failed kind=assets message=missing exe, checksum, or current path");
            _window.ShowUpdateBar(Available);
            _window.SetUpdateMessage(L.Get("Update_Fail"));
            return;
        }

        _applying = true;
        _applyCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _applyCancel = cancel;
        var dir = Path.Combine(SettingsService.AppDirectory, "updates");
        Directory.CreateDirectory(dir);
        var incoming = Path.Combine(dir, "Kutu.exe");
        var version = Available;
        try
        {
            var progress = new Progress<FetchProgress>(value => _window.ShowUpdateProgress(version, value));
            try
            {
                await ExternalToolFetch.DownloadFileAsync(exe.Url, incoming, progress, cancel.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UpdateStepException(UpdateFailure.Download(ex, File.Exists(incoming)), ex);
            }

            _window.SetUpdateMessage(L.Get("Update_Progress").Replace("{0}", "…"));
            try
            {
                var sums = await ExternalToolFetch.GetTextAsync(hash.Url, CancellationToken.None);
                var matches = Sha256Check.Matches(incoming, sums, "Kutu.exe");
                if (!File.Exists(incoming) || !matches)
                    throw new UpdateStepException(UpdateFailure.Hash(null, File.Exists(incoming), matches), new InvalidOperationException("hash"));
            }
            catch (UpdateStepException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UpdateStepException(UpdateFailure.Hash(ex, File.Exists(incoming), matches: false), ex);
            }

            var folder = Path.GetDirectoryName(current)!;
            var old = Path.Combine(folder, "Kutu.old.exe");
            try
            {
                if (File.Exists(old))
                {
                    try
                    {
                        File.Delete(old);
                    }
                    catch
                    {
                        old = Path.Combine(folder, "Kutu.old." + DateTime.UtcNow.Ticks + ".exe");
                    }
                }

                File.Move(current, old);
                try
                {
                    File.Copy(incoming, current, overwrite: true);
                }
                catch
                {
                    if (!File.Exists(current) && File.Exists(old))
                        File.Move(old, current);
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new UpdateStepException(UpdateFailure.Replace(ex), ex);
            }

            try
            {
                _releaseInstance();
                Process.Start(new ProcessStartInfo(current)
                {
                    Arguments = "--cleanup",
                    UseShellExecute = true
                });
                _host.Exit();
            }
            catch (Exception ex)
            {
                throw new UpdateStepException(UpdateFailure.Launch(ex), ex);
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (File.Exists(incoming))
                    File.Delete(incoming);
            }
            catch
            {
                // The cancelled download is removed on the next attempt.
            }

            if (!string.IsNullOrWhiteSpace(version))
                _window.ShowUpdateBar(version);
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(incoming))
                    File.Delete(incoming);
            }
            catch
            {
                // The rejected download is removed on the next attempt.
            }

            var kind = ex is UpdateStepException step ? step.Kind : UpdateFailureKind.Http;
            var detail = ex.InnerException ?? ex;
            AppLog.Write(
                "update failed kind=" + kind
                + " hresult=" + detail.HResult.ToString("X8")
                + " type=" + detail.GetType().Name
                + " message=" + detail.Message);
            _window.ShowUpdateBar(version);
            _window.SetUpdateMessage(FailureText(kind));
            if (kind == UpdateFailureKind.Blocked)
                _window.OfferReleasePage(UpdateFailure.ReleasePage(version));
        }
        finally
        {
            _applying = false;
        }
    }

    private static string FailureText(UpdateFailureKind kind) => kind switch
    {
        UpdateFailureKind.Http => L.Get("Update_FailHttp"),
        UpdateFailureKind.Hash => L.Get("Update_FailHash"),
        UpdateFailureKind.Blocked => L.Get("Update_FailBlocked"),
        UpdateFailureKind.Replace => L.Get("Update_FailReplace"),
        UpdateFailureKind.Relaunch => L.Get("Update_FailLaunch"),
        _ => L.Get("Update_Fail")
    };

    private sealed class UpdateStepException : Exception
    {
        public UpdateStepException(UpdateFailureKind kind, Exception inner) : base(inner.Message, inner) => Kind = kind;

        public UpdateFailureKind Kind { get; }
    }
}

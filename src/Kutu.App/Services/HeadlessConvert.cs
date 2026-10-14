using System.Windows.Threading;
using Kutu.Core.Commands;
using Kutu.Core.Diagnostics;
using Kutu.Core.Settings;
using Kutu.Sdk.Localization;

namespace Kutu.App.Services;

internal static class HeadlessConvert
{
    public static bool TryStart(string[] args)
    {
        if (!ConvertArgs.IsHeadless(args))
            return false;

        string message;
        try
        {
            var settings = new SettingsService(new DpapiProtector());
            try
            {
                settings.LoadAsync().GetAwaiter().GetResult();
            }
            catch
            {
                settings = new SettingsService(new DpapiProtector());
            }

            L.SetCulture(string.IsNullOrWhiteSpace(settings.Current.Language) ? "tr" : settings.Current.Language);
            if (!ConvertArgs.TryParse(args, out var format, out var files))
                message = L.Get("Image_Failed");
            else
                message = ConvertCommand.Execute(format, files, settings.Current.JpegQuality <= 0 ? 85 : settings.Current.JpegQuality);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            message = L.Get("Image_Failed");
        }

        try
        {
            ShowBalloon(message);
        }
        catch
        {
            // The balloon is optional. The files are already written.
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            System.Windows.Application.Current?.Shutdown();
        };
        timer.Start();
        // Leave even if the dispatcher never pumps (no window was created).
        _ = Task.Run(async () =>
        {
            await Task.Delay(2500);
            Environment.Exit(0);
        });
        return true;
    }

    private static void ShowBalloon(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        System.Drawing.Icon? owned = null;
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
                owned = System.Drawing.Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            owned = null;
        }

        var notify = new System.Windows.Forms.NotifyIcon
        {
            Icon = owned ?? System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Kutu",
            BalloonTipTitle = "Kutu",
            BalloonTipText = message.Length > 240 ? message[..240] : message
        };
        notify.ShowBalloonTip(2000);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            notify.Visible = false;
            notify.Dispose();
            owned?.Dispose();
        };
        timer.Start();
    }
}

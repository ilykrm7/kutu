using System.Windows;
using Kutu.Core;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;

namespace Kutu.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly IKutuHost _host;
    private readonly System.Windows.Forms.NotifyIcon _icon;

    public TrayService(IKutuHost host)
    {
        _host = host;
        var icon = LoadIcon();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(L.Get("Tray_Open"), null, (_, _) => Run(_host.ShowAndActivate));
        menu.Items.Add(L.Get("Tray_Shot"), null, (_, _) => Run(_host.StartScreenshot));
        menu.Items.Add(L.Get("Nav_Settings"), null, (_, _) => Run(_host.ShowSettings));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(L.Get("Tray_Exit"), null, (_, _) => Run(_host.Exit));
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = "Kutu",
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => Run(_host.ShowAndActivate);
    }

    public void ShowNotice(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            _icon.BalloonTipTitle = "Kutu";
            _icon.BalloonTipText = message.Length > 240 ? message[..240] : message;
            _icon.ShowBalloonTip(4000);
        }
        catch
        {
            // The tray icon can be unavailable while the app is closing.
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private static void Run(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var path = HostProcess.Executable;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var extracted = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                    return extracted;
            }
        }
        catch
        {
            // Fall through to the default icon.
        }

        return System.Drawing.SystemIcons.Application;
    }
}

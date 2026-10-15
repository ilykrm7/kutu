using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Kutu.App.Services;
using Kutu.App.Views;
using Kutu.Core.IO;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;

namespace Kutu.App;

public partial class MainWindow : FluentWindow
{
    private readonly IKutuHost _host;
    private DashboardView? _dashboard;
    private SettingsView? _settings;
    private LauncherWindow? _launcher;
    private DispatcherTimer? _toastTimer;
    private bool _exit;

    public MainWindow(IKutuHost host)
    {
        _host = host;
        InitializeComponent();
        DashboardButton.Content = NavLabel("Home24", L.Get("Nav_Dashboard"));
        FavoritesButton.Content = NavLabel("Star24", L.Get("Nav_Favorites"));
        SettingsButton.Content = NavLabel("Settings24", L.Get("Nav_Settings"));
        UpdateApplyButton.Content = L.Get("Update_Action");
        SourceInitialized += (_, _) => ApplyDarkChrome();
        ShowDashboard();
    }

    public void ShowDashboard() => ShowPage(PageKind.Tools);

    public void ShowFavorites() => ShowPage(PageKind.Favorites);

    public void ShowSettings() => ShowPage(PageKind.Settings);

    public void ShowTool(IKutuTool tool, UIElement view)
    {
        var page = new ToolHostView(tool, view, ShowDashboard);
        Present(page);
        ClearNav();
    }

    public void ToggleLauncher(IKutuHost host)
    {
        if (_launcher is { IsVisible: true })
        {
            _launcher.Close();
            return;
        }

        if (_launcher is null)
        {
            _launcher = new LauncherWindow(host);
            _launcher.Closed += (_, _) => _launcher = null;
        }

        _launcher.Present();
    }

    public void ShowUpdateBar(string version)
    {
        UpdateText.Text = string.Format(L.Get("Update_Available"), version);
        UpdateApplyButton.Content = L.Get("Update_Action");
        UpdateApplyButton.Visibility = Visibility.Visible;
        UpdateProgress.Visibility = Visibility.Collapsed;
        UpdateProgress.Value = 0;
        UpdateBar.Visibility = Visibility.Visible;
    }

    public void ShowUpdateProgress(string version, FetchProgress progress)
    {
        var fraction = progress.Fraction;
        if (fraction < 0)
            fraction = 0;
        if (fraction > 1)
            fraction = 1;
        UpdateText.Text = TransferStatus.Format(progress.Received, progress.Total, progress.BytesPerSecond);
        UpdateApplyButton.Visibility = Visibility.Collapsed;
        UpdateProgress.IsIndeterminate = progress.Total <= 0;
        UpdateProgress.Visibility = Visibility.Visible;
        UpdateProgress.Value = fraction;
        UpdateBar.Visibility = Visibility.Visible;
    }

    public void SetUpdateMessage(string message)
    {
        UpdateText.Text = message;
        UpdateApplyButton.Content = L.Get("Update_Action");
        UpdateApplyButton.Visibility = Visibility.Visible;
        UpdateBar.Visibility = Visibility.Visible;
    }

    public void HideUpdateBar()
    {
        UpdateBar.Visibility = Visibility.Collapsed;
        UpdateProgress.Visibility = Visibility.Collapsed;
        UpdateProgress.Value = 0;
    }

    public void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastCard.Visibility = Visibility.Visible;
        _toastTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _toastTimer.Stop();
        _toastTimer.Tick -= HideToast;
        _toastTimer.Tick += HideToast;
        _toastTimer.Start();
    }

    public void ExitApplication()
    {
        _exit = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void ShowPage(PageKind kind)
    {
        ClearNav();
        if (kind == PageKind.Settings)
        {
            _settings ??= new SettingsView(_host);
            _settings.LoadFromSettings();
            Present(_settings);
            SettingsButton.Tag = "on";
            return;
        }

        _dashboard ??= new DashboardView(_host);
        if (kind == PageKind.Favorites)
            _dashboard.ShowFavorites();
        else
            _dashboard.ShowAll();
        Present(_dashboard);
        if (kind == PageKind.Favorites)
            FavoritesButton.Tag = "on";
        else
            DashboardButton.Tag = "on";
    }

    private void Present(FrameworkElement page)
    {
        Pages.Content = page;
        Motion.Enter(page);
    }

    private void ClearNav()
    {
        DashboardButton.Tag = null;
        FavoritesButton.Tag = null;
        SettingsButton.Tag = null;
    }

    private void HideToast(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        ToastCard.Visibility = Visibility.Collapsed;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => ShowDashboard();

    private void Favorites_Click(object sender, RoutedEventArgs e) => ShowFavorites();

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private void UpdateApply_Click(object sender, RoutedEventArgs e) => UpdateController.Current?.Apply();

    private void UpdateClose_Click(object sender, RoutedEventArgs e) => UpdateController.Current?.DismissBar();

    private void ApplyDarkChrome()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        var caption = 0x00140B07;
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
        var text = 0x00FBF7F4;
        DwmSetWindowAttribute(hwnd, 36, ref text, sizeof(int));
    }

    private static StackPanel NavLabel(string symbol, string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new SymbolIcon
        {
            Symbol = Chrome.Symbol(symbol),
            FontSize = 18,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.FindResource("KutuTextBrush")
        });
        return panel;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private enum PageKind
    {
        Tools,
        Favorites,
        Settings
    }
}

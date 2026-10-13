using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Kutu.App.Views;
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
        FooterLink.Text = "Kerim Ekşi · kerimeksi.com";
        FooterLink.MouseLeftButtonUp += (_, _) => OpenSite();
        var version = typeof(MainWindow).Assembly.GetName().Version;
        VersionText.Text = version is null ? "v1.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        DashboardButton.Content = NavLabel("Home24", L.Get("Nav_Dashboard"));
        FavoritesButton.Content = NavLabel("Star24", L.Get("Nav_Favorites"));
        SettingsButton.Content = NavLabel("Settings24", L.Get("Nav_Settings"));
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

        _launcher = new LauncherWindow(host);
        _launcher.Closed += (_, _) => _launcher = null;
        _launcher.Show();
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

    private static void OpenSite()
    {
        Process.Start(new ProcessStartInfo("https://kerimeksi.com") { UseShellExecute = true });
    }

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

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Kutu.App.Views;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
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
        TaglineText.Text = L.Get("App_Tagline");
        VersionText.Text = "v1.0";
        DashboardButton.Content = NavLabel("Home24", L.Get("Nav_Dashboard"));
        SettingsButton.Content = NavLabel("Settings24", L.Get("Nav_Settings"));
        ShowDashboard();
    }

    public void ShowDashboard()
    {
        _dashboard ??= new DashboardView(_host);
        _dashboard.Refresh();
        Pages.Content = _dashboard;
        DashboardButton.Tag = "on";
        SettingsButton.Tag = null;
    }

    public void ShowSettings()
    {
        _settings ??= new SettingsView(_host);
        _settings.LoadFromSettings();
        Pages.Content = _settings;
        SettingsButton.Tag = "on";
        DashboardButton.Tag = null;
    }

    public void ShowTool(IKutuTool tool, UIElement view)
    {
        Pages.Content = new ToolHostView(tool, view, ShowDashboard);
        DashboardButton.Tag = null;
        SettingsButton.Tag = null;
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

    private void HideToast(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        ToastCard.Visibility = Visibility.Collapsed;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => ShowDashboard();

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private static StackPanel NavLabel(string symbol, string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new SymbolIcon
        {
            Symbol = ChromeSymbol(symbol),
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

    private static Wpf.Ui.Controls.SymbolRegular ChromeSymbol(string name) =>
        Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>(name, out var symbol)
            ? symbol
            : Wpf.Ui.Controls.SymbolRegular.Circle24;
}

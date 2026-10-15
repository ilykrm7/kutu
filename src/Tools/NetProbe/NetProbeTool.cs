using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Network;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.NetProbe;

public sealed class NetProbeTool : IKutuTool
{
    public string Id => "net-probe";

    public string TitleKey => "Tool_Ping_Title";

    public string DescriptionKey => "Tool_Ping_Desc";

    public string Symbol => "Wifi124";

    public IReadOnlyList<string> Aliases { get; } = ["ping", "port", "tarama", "scan", "nmap", "rdp", "ssh"];

    public int Order => 74;

    public UIElement CreateView(IKutuHost host) => new NetProbeView(host);
}

public sealed class NetProbeView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _hostBox;
    private readonly TextBox _count;
    private readonly TextBox _ports;
    private readonly TextBox _timeout;
    private readonly TextBox _concurrency;
    private readonly TextBlock _pingOut;
    private readonly TextBlock _progress;
    private readonly TextBlock _empty;
    private readonly Grid _header;
    private readonly StackPanel _rows;
    private readonly Button[] _modes;
    private readonly List<PortScanResult> _results = [];
    private string _preset = "common";
    private CancellationTokenSource? _pingCancel;
    private CancellationTokenSource? _scanCancel;

    public NetProbeView(IKutuHost host)
    {
        _host = host;
        _hostBox = Chrome.Input("1.1.1.1");
        _hostBox.Text = "1.1.1.1";
        _hostBox.Width = 220;
        _count = Small("4");
        _ports = Small("80");
        _ports.Width = 140;
        _timeout = Small("200");
        _concurrency = Small("512");
        _pingOut = Chrome.Hint("");
        _progress = Chrome.Hint("");
        _empty = new TextBlock
        {
            Text = L.Get("Port_None"),
            Foreground = Chrome.Muted,
            FontSize = 13,
            Opacity = 0.8,
            Margin = new Thickness(0, 8, 0, 8),
            Visibility = Visibility.Collapsed
        };
        _header = Header();
        _header.Visibility = Visibility.Collapsed;
        _rows = new StackPanel();
        _modes =
        [
            Mode("single", L.Get("Port_Single")),
            Mode("range", L.Get("Port_Range")),
            Mode("common", L.Get("Port_Common")),
            Mode("all", L.Get("Port_All"))
        ];
        ShowPreset("common");

        var pingRow = Row(_hostBox, Chrome.Primary(L.Get("Action_Ping"), SymbolRegular.Wifi124, async (_, _) => await PingAsync()), _count, Chrome.Secondary(L.Get("Action_Cancel"), SymbolRegular.Dismiss24, (_, _) => _pingCancel?.Cancel()));
        var modes = new Border
        {
            BorderBrush = Chrome.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(4),
            Margin = new Thickness(0, 4, 0, 10),
            Child = Row(_modes)
        };
        var scanRow = Row(
            _ports,
            _timeout,
            _concurrency,
            Chrome.Primary(L.Get("Action_Scan"), SymbolRegular.ScanDash24, async (_, _) => await ScanAsync()),
            Chrome.Secondary(L.Get("Action_Cancel"), SymbolRegular.Dismiss24, (_, _) => _scanCancel?.Cancel()));
        var export = Row(
            Chrome.Secondary(L.Get("Action_Copy"), SymbolRegular.Copy24, (_, _) => Copy()),
            Chrome.Secondary(L.Get("Action_Export"), SymbolRegular.ArrowDownload24, (_, _) => Export()));

        var stack = new StackPanel();
        stack.Children.Add(Chrome.Heading(L.Get("Tool_Ping_Title")));
        stack.Children.Add(Chrome.Label(L.Get("Ping_Host")));
        stack.Children.Add(pingRow);
        stack.Children.Add(_pingOut);
        stack.Children.Add(Chrome.Label(L.Get("Port_Mode")));
        stack.Children.Add(modes);
        stack.Children.Add(scanRow);
        stack.Children.Add(_progress);
        stack.Children.Add(_header);
        stack.Children.Add(_rows);
        stack.Children.Add(_empty);
        stack.Children.Add(export);
        Content = Chrome.ScrollPage(stack, 980);
    }

    private async Task PingAsync()
    {
        _pingCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _pingCancel = cancel;
        _pingOut.Text = "";
        if (!int.TryParse(_count.Text.Trim(), out var count))
            count = 4;
        count = Math.Max(1, Math.Min(count, 20));
        var times = new List<long>();
        var lost = 0;
        using var ping = new Ping();
        for (var i = 1; i <= count && !cancel.IsCancellationRequested; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(_hostBox.Text.Trim(), 1500);
                if (reply.Status == IPStatus.Success)
                {
                    times.Add(reply.RoundtripTime);
                    _pingOut.Text += $"{i}. {reply.RoundtripTime} ms{Environment.NewLine}";
                }
                else
                {
                    lost++;
                    _pingOut.Text += $"{i}. {L.Get("Ping_Timeout")}{Environment.NewLine}";
                }
            }
            catch (Exception ex)
            {
                lost++;
                _pingOut.Text += $"{i}. {ex.Message}{Environment.NewLine}";
            }
        }

        if (times.Count > 0)
            _pingOut.Text += string.Format(L.Get("Ping_Summary"), times.Min(), (int)times.Average(), times.Max(), lost);
    }

    private async Task ScanAsync()
    {
        _scanCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _scanCancel = cancel;
        if (!PortPlan.TryBuild(_preset, _ports.Text, out var ports, out _))
        {
            _progress.Text = L.Get("Port_Invalid");
            return;
        }

        var host = _hostBox.Text.Trim();
        if (host.Length == 0)
        {
            _progress.Text = L.Get("Port_Invalid");
            return;
        }

        if (!int.TryParse(_timeout.Text.Trim(), out var timeout))
            timeout = 200;
        if (!int.TryParse(_concurrency.Text.Trim(), out var concurrency))
            concurrency = 512;
        _results.Clear();
        _rows.Children.Clear();
        _empty.Visibility = Visibility.Collapsed;
        _header.Visibility = Visibility.Collapsed;
        var progress = new Progress<PortScanTick>(tick =>
        {
            var eta = tick.Eta.TotalHours >= 1 ? tick.Eta.ToString(@"h\:mm\:ss") : tick.Eta.ToString(@"mm\:ss");
            _progress.Text = string.Format(L.Get("Port_Progress"), tick.Done, tick.Total, tick.Open, eta);
        });
        await PortScanner.RunAsync(host, ports, timeout, concurrency, result =>
        {
            if (result.Open)
                Dispatcher.Invoke(() => AddRow(result));
        }, progress, cancel.Token);
        if (!cancel.IsCancellationRequested && _results.All(result => !result.Open))
        {
            _rows.Children.Clear();
            _header.Visibility = Visibility.Collapsed;
            _empty.Visibility = Visibility.Visible;
        }
    }

    private void AddRow(PortScanResult result)
    {
        if (!result.Open)
            return;

        _empty.Visibility = Visibility.Collapsed;
        _header.Visibility = Visibility.Visible;
        if (result.Port == 3389)
            _results.Insert(0, result);
        else if (result.Port == 22)
            _results.Insert(_results.Count > 0 && _results[0].Port == 3389 ? 1 : 0, result);
        else
            _results.Add(result);
        Render();
    }

    private void Render()
    {
        _rows.Children.Clear();
        foreach (var result in _results)
            _rows.Children.Add(Line(result));
    }

    private UIElement Line(PortScanResult result)
    {
        var grid = TableGrid();
        grid.Children.Add(Cell(result.Port.ToString(), 0));
        grid.Children.Add(Cell(result.Open ? L.Get("Port_Open") : L.Get("Port_Closed"), 1));
        grid.Children.Add(Cell(result.Service.Length == 0 ? "—" : result.Service, 2));
        grid.Children.Add(Cell(result.Banner.Length == 0 ? "—" : result.Banner, 3));
        return grid;
    }

    private static Grid Header()
    {
        var grid = TableGrid();
        grid.Children.Add(Head(L.Get("Port_HeaderPort"), 0));
        grid.Children.Add(Head(L.Get("Port_HeaderState"), 1));
        grid.Children.Add(Head(L.Get("Port_HeaderService"), 2));
        grid.Children.Add(Head(L.Get("Port_HeaderBanner"), 3));
        return grid;
    }

    private void Copy()
    {
        _host.CopyText(TableText());
        _host.Toast(L.Get("Action_Copied"));
    }

    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "kutu-ports.csv" };
        if (dialog.ShowDialog() != true)
            return;
        var builder = new StringBuilder();
        builder.AppendLine("port,state,service,banner");
        foreach (var result in OpenResults())
        {
            builder.Append(result.Port).Append(',')
                .Append(BulkCsv.Cell("open")).Append(',')
                .Append(BulkCsv.Cell(result.Service)).Append(',')
                .Append(BulkCsv.Cell(result.Banner)).AppendLine();
        }

        File.WriteAllText(dialog.FileName, builder.ToString());
        _host.Toast(L.Get("Action_Save"));
    }

    private string TableText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("port\tstate\tservice\tbanner");
        foreach (var result in OpenResults())
            builder.Append(result.Port).Append('\t').Append("open").Append('\t').Append(result.Service).Append('\t').Append(result.Banner).AppendLine();
        return builder.ToString();
    }

    private IEnumerable<PortScanResult> OpenResults() => _results.Where(result => result.Open);

    private Button Mode(string id, string label)
    {
        var button = new Button
        {
            Content = label,
            Tag = id,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(2, 0, 2, 0),
            FontSize = 13
        };
        button.Click += (_, _) =>
        {
            _preset = id;
            ShowPreset(id);
            if (id == "all")
            {
                _ports.Text = "1-65535";
                _timeout.Text = "200";
                _concurrency.Text = "512";
            }
            else if (id == "common")
            {
                _ports.Text = "";
            }
        };
        Motion.Pressable(button);
        return button;
    }

    private void ShowPreset(string id)
    {
        foreach (var button in _modes)
        {
            var on = Equals(button.Tag as string, id);
            button.Appearance = on ? ControlAppearance.Primary : ControlAppearance.Secondary;
            button.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
            button.Icon = on ? new SymbolIcon { Symbol = SymbolRegular.Checkmark24, FontSize = 14 } : null;
        }
    }

    private static TextBox Small(string text)
    {
        var box = Chrome.Input(text);
        box.Text = text;
        box.Width = 88;
        box.Margin = new Thickness(0, 0, 8, 0);
        return box;
    }

    private static StackPanel Row(params UIElement[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var child in children)
            panel.Children.Add(child);
        return panel;
    }

    private static Grid TableGrid()
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        return grid;
    }

    private static TextBlock Head(string text, int column)
    {
        var block = new TextBlock { Text = text, Foreground = Chrome.Muted, FontWeight = FontWeights.SemiBold, FontSize = 12 };
        Grid.SetColumn(block, column);
        return block;
    }

    private static TextBlock Cell(string text, int column)
    {
        var block = new TextBlock { Text = text, Foreground = Chrome.Text, FontSize = 13, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(block, column);
        return block;
    }
}

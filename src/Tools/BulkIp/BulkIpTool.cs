using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Kutu.Core.Network;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Kutu.Tools.IpReputation;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.BulkIp;

public sealed class BulkIpTool : IKutuTool
{
    public string Id => "bulk-ip";

    public string TitleKey => "Tool_Bulk_Title";

    public string DescriptionKey => "Tool_Bulk_Desc";

    public string Symbol => "PeopleTeam24";

    public IReadOnlyList<string> Aliases { get; } = ["bulk", "toplu", "ipler", "ip list", "reputation"];

    public int Order => 82;

    public UIElement CreateView(IKutuHost host) => new BulkIpView(host);
}

public sealed class BulkIpView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _input;
    private readonly TextBlock _status;
    private readonly StackPanel _rows;
    private readonly List<BulkIpExport> _rowsData = [];
    private CancellationTokenSource? _cancel;

    public BulkIpView(IKutuHost host)
    {
        _host = host;
        _input = Chrome.Input(L.Get("Bulk_Paste"), multiline: true);
        _input.MinHeight = 120;
        _status = Chrome.Hint("");
        _rows = new StackPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
        buttons.Children.Add(Chrome.Primary(L.Get("Action_Check"), SymbolRegular.Shield24, async (_, _) => await RunAsync()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Bulk_Mine"), SymbolRegular.Person24, async (_, _) => await MineAsync()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Bulk_Load"), SymbolRegular.FolderOpen24, (_, _) => Load()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Cancel"), SymbolRegular.Dismiss24, (_, _) => _cancel?.Cancel()));
        buttons.Children.Add(Chrome.Secondary(L.Get("Action_Export"), SymbolRegular.ArrowDownload24, (_, _) => Export()));
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Hint(L.Get("Ip_SymantecNote")));
        stack.Children.Add(_input);
        stack.Children.Add(buttons);
        stack.Children.Add(_status);
        stack.Children.Add(Header());
        stack.Children.Add(_rows);
        Content = Chrome.ScrollPage(stack, 980);
    }

    private async Task MineAsync()
    {
        try
        {
            var ip = await IpLookup.PublicAddressAsync();
            if (_input.Text.IndexOf(ip, StringComparison.OrdinalIgnoreCase) < 0)
                _input.Text = string.IsNullOrWhiteSpace(_input.Text) ? ip : _input.Text.TrimEnd() + Environment.NewLine + ip;
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private void Load()
    {
        var dialog = new OpenFileDialog { Filter = "Text|*.txt;*.csv;*.log|All|*.*" };
        if (dialog.ShowDialog() != true)
            return;
        _input.Text = File.ReadAllText(dialog.FileName);
    }

    private async Task RunAsync()
    {
        _cancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        var ips = IpList.Parse(_input.Text);
        _rows.Children.Clear();
        _rowsData.Clear();
        if (ips.Count == 0)
        {
            _status.Text = L.Get("Bulk_Empty");
            return;
        }

        var gate = new SemaphoreSlim(3);
        var done = 0;
        var tasks = ips.Select(async ip =>
        {
            await gate.WaitAsync(cancel.Token);
            try
            {
                if (cancel.IsCancellationRequested)
                    return;
                var outcomes = await IpLookup.CheckAllAsync(ip, _host.Settings, cancel.Token);
                var row = Map(ip, outcomes);
                var current = Interlocked.Increment(ref done);
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    _rowsData.Add(row);
                    _rows.Children.Add(Line(row));
                    _status.Text = string.Format(L.Get("Bulk_Working"), current + "/" + ips.Count);
                }));
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _status.Text = L.Get("Action_Cancel");
        }
    }

    private static BulkIpExport Map(string ip, IEnumerable<ProviderOutcome> outcomes)
    {
        var list = outcomes.ToList();
        var score = IpRisk.Combine(
            list.Where(item => item.Id is not "dnsbl" and not "tor" and not "symantec").Select(item => item.Score).ToList(),
            list.FirstOrDefault(item => item.Id == "dnsbl")?.DnsblHits,
            list.FirstOrDefault(item => item.Id == "tor")?.Tor);
        string? Pick(string id, Func<ProviderOutcome, string?> read) =>
            list.Where(item => item.Id == id).Select(read).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var dnsbl = list.FirstOrDefault(item => item.Id == "dnsbl")?.DnsblHits?.ToString() ?? "";
        var tor = list.FirstOrDefault(item => item.Id == "tor")?.Tor switch
        {
            true => "yes",
            false => "no",
            _ => ""
        };
        return new BulkIpExport(
            ip,
            score?.ToString() ?? "",
            Pick("geo", item => item.Country) ?? "",
            Pick("geo", item => item.City) ?? "",
            Pick("geo", item => item.Isp) ?? "",
            Pick("geo", item => item.Asn) ?? "",
            dnsbl,
            tor,
            SymantecReview.Url(ip));
    }

    private UIElement Line(BulkIpExport row)
    {
        var grid = Table();
        Add(grid, row.Ip, 0);
        Add(grid, row.Risk, 1);
        Add(grid, row.Country, 2);
        Add(grid, row.Isp, 3);
        Add(grid, row.Dnsbl, 4);
        var link = new TextBlock
        {
            Text = L.Get("Action_OpenBrowser"),
            Foreground = Chrome.Text,
            TextDecorations = TextDecorations.Underline,
            Cursor = System.Windows.Input.Cursors.Hand,
            FontSize = 13
        };
        link.MouseLeftButtonUp += (_, _) => Process.Start(new ProcessStartInfo(row.SymantecUrl) { UseShellExecute = true });
        Grid.SetColumn(link, 5);
        grid.Children.Add(link);
        return grid;
    }

    private static Grid Header()
    {
        var grid = Table();
        Add(grid, "IP", 0, true);
        Add(grid, "Risk", 1, true);
        Add(grid, "Country", 2, true);
        Add(grid, "ISP", 3, true);
        Add(grid, "DNSBL", 4, true);
        Add(grid, "Symantec", 5, true);
        return grid;
    }

    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "kutu-ip.csv" };
        if (dialog.ShowDialog() != true)
            return;
        File.WriteAllText(dialog.FileName, BulkCsv.Write(_rowsData), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _host.Toast(L.Get("Action_Save"));
    }

    private static Grid Table()
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        foreach (var width in new[] { 150, 70, 90, 180, 70, 140 })
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        return grid;
    }

    private static void Add(Grid grid, string text, int column, bool head = false)
    {
        var block = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(text) ? "—" : text,
            Foreground = head ? Chrome.Muted : Chrome.Text,
            FontWeight = head ? FontWeights.SemiBold : FontWeights.Normal,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }
}

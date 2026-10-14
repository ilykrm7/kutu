using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kutu.Core.Network;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.IpReputation;

public sealed class IpReputationTool : IKutuTool
{
    public string Id => "ip-reputation";

    public string TitleKey => "Tool_Ip_Title";

    public string DescriptionKey => "Tool_Ip_Desc";

    public string Symbol => "Shield24";

    public IReadOnlyList<string> Aliases { get; } = ["ip", "abuse", "fraud", "itibar", "asn", "geo", "reputation"];

    public int Order => 80;

    public UIElement CreateView(IKutuHost host) => new IpReputationView(host);
}

public sealed class IpReputationView : UserControl
{
    private readonly IKutuHost _host;
    private readonly TextBox _ip;
    private readonly StackPanel _body;

    public IpReputationView(IKutuHost host)
    {
        _host = host;
        _ip = Chrome.Input("8.8.8.8");
        _ip.MinWidth = 220;
        _ip.Margin = new Thickness(0, 0, 8, 0);
        _ip.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                await CheckAsync();
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_ip);
        row.Children.Add(Chrome.Primary(L.Get("Action_Check"), SymbolRegular.Shield24, async (_, _) => await CheckAsync()));
        row.Children.Add(Chrome.Secondary(L.Get("Ip_MyIp"), SymbolRegular.Person24, async (_, _) => await UseMineAsync()));
        var form = new StackPanel();
        form.Children.Add(Chrome.Label(L.Get("Ip_Address")));
        form.Children.Add(row);
        _body = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Section(L.Get("Tool_Ip_Title"), SymbolRegular.Shield24, form));
        stack.Children.Add(_body);
        Content = stack;
    }

    private async Task UseMineAsync()
    {
        _body.Children.Clear();
        _body.Children.Add(Chrome.Loading(L.Get("Common_Working")));
        try
        {
            _ip.Text = await IpLookup.PublicAddressAsync();
            await CheckAsync();
        }
        catch (Exception ex)
        {
            _body.Children.Clear();
            _body.Children.Add(Chrome.Hint(ex.Message));
        }
    }

    private async Task CheckAsync()
    {
        _body.Children.Clear();
        if (!System.Net.IPAddress.TryParse(_ip.Text.Trim(), out var address))
        {
            _body.Children.Add(Chrome.Hint(L.Get("Ip_Invalid")));
            return;
        }

        _body.Children.Add(Chrome.Loading(L.Get("Ip_Working")));
        var results = await IpLookup.CheckAllAsync(address.ToString(), _host.Settings);
        _body.Children.Clear();
        var summary = BuildSummary(address.ToString(), results);
        Motion.Enter(summary);
        _body.Children.Add(summary);
        var index = 0;
        foreach (var result in results)
        {
            var card = ProviderCard(result);
            card.Margin = new Thickness(0, 0, 0, 12);
            Motion.Enter(card, Math.Min(index * 35, 220));
            _body.Children.Add(card);
            index++;
        }
    }

    private FrameworkElement BuildSummary(string ip, IEnumerable<ProviderOutcome> results)
    {
        var list = results.ToList();
        var geo = list.FirstOrDefault(item => item.Id == "geo");
        var score = IpRisk.Combine(
            list.Where(item => item.Id is not "dnsbl" and not "tor").Select(item => item.Score).ToList(),
            list.FirstOrDefault(item => item.Id == "dnsbl")?.DnsblHits,
            list.FirstOrDefault(item => item.Id == "tor")?.Tor);
        var level = IpRisk.Level(score);
        var (label, color) = level switch
        {
            "high" => (L.Get("Ip_RiskHigh"), Color.FromRgb(0xFF, 0x6B, 0x6B)),
            "medium" => (L.Get("Ip_RiskMedium"), Color.FromRgb(0xFF, 0xC8, 0x57)),
            "low" => (L.Get("Ip_RiskLow"), Color.FromRgb(0x4B, 0xDF, 0xB3)),
            _ => (L.Get("Ip_RiskInfo"), Color.FromRgb(0x93, 0xA0, 0xB8))
        };
        var badge = new Border
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                Text = score is int value ? $"{label}  {value}" : label,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black
            }
        };
        var panel = new StackPanel();
        panel.Children.Add(badge);
        panel.Children.Add(Chrome.ValueRow(L.Get("Ip_Address"), ip, () => Copy(ip)));
        panel.Children.Add(Chrome.ValueRow(L.Get("Ip_Country"), First(list, item => item.Country) ?? geo?.Country ?? "—", null));
        panel.Children.Add(Chrome.ValueRow(L.Get("Ip_City"), First(list, item => item.City) ?? "—", null));
        panel.Children.Add(Chrome.ValueRow(L.Get("Ip_Isp"), First(list, item => item.Isp) ?? "—", null));
        panel.Children.Add(Chrome.ValueRow(L.Get("Ip_Asn"), First(list, item => item.Asn) ?? "—", null));
        return Chrome.Section(L.Get("Ip_Summary"), SymbolRegular.Shield24, panel);
    }

    private FrameworkElement ProviderCard(ProviderOutcome result)
    {
        var panel = new StackPanel();
        if (result.Status == "missing")
        {
            var hint = new TextBlock
            {
                Text = L.Get("Ip_AddKey"),
                Foreground = Chrome.Muted,
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                TextDecorations = TextDecorations.Underline
            };
            hint.MouseLeftButtonUp += (_, _) => _host.ShowSettings();
            panel.Children.Add(hint);
        }
        else
        {
            foreach (var line in result.Lines.Where(item => item.Label.Length > 0))
                panel.Children.Add(Chrome.ValueRow(line.Label, line.Value, () => Copy(line.Value)));
            if (result.Lines.Count == 0)
                panel.Children.Add(Chrome.Hint(result.Status == "empty" ? L.Get("Ip_NoRecord") : L.Get("Ip_Clean")));
        }

        var card = Chrome.Section(result.Title + "  ·  " + StatusText(result.Status), SymbolRegular.Glance24, panel);
        return card;
    }

    private void Copy(string value)
    {
        _host.CopyText(value);
        _host.Toast(L.Get("Action_Copied"));
    }

    private static string? First(IEnumerable<ProviderOutcome> results, Func<ProviderOutcome, string?> pick) =>
        results.Select(pick).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string StatusText(string status) => status switch
    {
        "missing" => "",
        "risk" => "Risk",
        "clean" => "Temiz",
        "empty" => "Kayıt yok",
        "error" => "Ulaşılamadı",
        _ => "Tamam"
    };
}

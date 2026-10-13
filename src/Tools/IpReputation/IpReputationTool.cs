using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly TextBlock _status;
    private readonly StackPanel _result;

    public IpReputationView(IKutuHost host)
    {
        _host = host;
        _ip = Chrome.Input("8.8.8.8");
        _ip.Width = 280;
        _ip.Margin = new Thickness(0, 0, 8, 0);
        _ip.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                await CheckAsync();
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        row.Children.Add(_ip);
        row.Children.Add(Chrome.Primary(L.Get("Action_Check"), SymbolRegular.Shield24, async (_, _) => await CheckAsync()));
        _status = Chrome.Hint("");
        _result = new StackPanel();
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Label(L.Get("Ip_Address")));
        stack.Children.Add(row);
        stack.Children.Add(_status);
        stack.Children.Add(_result);
        Content = stack;
    }

    private async Task CheckAsync()
    {
        _result.Children.Clear();
        var text = _ip.Text.Trim();
        if (!IPAddress.TryParse(text, out var address))
        {
            _status.Text = L.Get("Ip_Invalid");
            return;
        }

        _status.Text = L.Get("Common_Working");
        try
        {
            var report = await IpLookup.CheckAsync(address.ToString(), _host.Settings.AbuseIpDbKey);
            _status.Text = report.AbuseScore is null ? L.Get("Ip_NeedKey") : "";
            var panel = new StackPanel();
            if (report.AbuseScore is int score)
            {
                panel.Children.Add(ScoreBadge(score));
                panel.Children.Add(Line(L.Get("Ip_Reports"), report.Reports?.ToString() ?? L.Get("Common_None")));
                panel.Children.Add(Line(L.Get("Ip_Usage"), Blank(report.Usage)));
            }

            panel.Children.Add(Line(L.Get("Ip_Country"), Blank(report.Country)));
            panel.Children.Add(Line(L.Get("Ip_City"), Blank(report.City)));
            panel.Children.Add(Line(L.Get("Ip_Isp"), Blank(report.Isp)));
            panel.Children.Add(Line(L.Get("Ip_Asn"), Blank(report.Asn)));
            _result.Children.Add(Chrome.Card(panel));
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private static UIElement ScoreBadge(int score)
    {
        var color = score >= 75 ? Color.FromRgb(0xFF, 0x6B, 0x6B) : score >= 25 ? Color.FromRgb(0xFF, 0xC8, 0x57) : Color.FromRgb(0x4B, 0xDF, 0xB3);
        var badge = new Border
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16, 10, 16, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14),
            Child = new TextBlock
            {
                Text = $"{L.Get("Ip_Score")}  {score}",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            }
        };
        return badge;
    }

    private static UIElement Line(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var l = Chrome.Label(label);
        l.Margin = new Thickness(0);
        var v = new TextBlock { Text = value, Foreground = Chrome.Text, TextWrapping = TextWrapping.Wrap, FontSize = 14 };
        Grid.SetColumn(v, 1);
        grid.Children.Add(l);
        grid.Children.Add(v);
        return grid;
    }

    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? L.Get("Common_None") : value;
}

public sealed class IpReport
{
    public int? AbuseScore { get; init; }

    public int? Reports { get; init; }

    public string? Usage { get; init; }

    public string? Country { get; init; }

    public string? City { get; init; }

    public string? Isp { get; init; }

    public string? Asn { get; init; }
}

public static class IpLookup
{
    public static async Task<IpReport> CheckAsync(string ip, string? abuseKey, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Kutu/1.0");
        int? score = null;
        int? reports = null;
        string? usage = null;
        string? country = null;
        string? isp = null;
        if (!string.IsNullOrWhiteSpace(abuseKey))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.abuseipdb.com/api/v2/check?ipAddress={Uri.EscapeDataString(ip)}&maxAgeInDays=90");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Key", abuseKey);
            using var response = await http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(Trim(body));

            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("abuseConfidenceScore", out var scoreEl) && scoreEl.TryGetInt32(out var parsed))
                    score = parsed;
                if (data.TryGetProperty("totalReports", out var reportsEl) && reportsEl.TryGetInt32(out var parsedReports))
                    reports = parsedReports;
                usage = ReadString(data, "usageType");
                country = ReadString(data, "countryCode");
                isp = ReadString(data, "isp");
            }
        }

        string? city = null;
        string? asn = null;
        string? geoCountry = null;
        string? geoIsp = null;
        try
        {
            var geoJson = await http.GetStringAsync($"https://ipwho.is/{Uri.EscapeDataString(ip)}", cancellationToken);
            using var geo = JsonDocument.Parse(geoJson);
            var root = geo.RootElement;
            if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True)
            {
                geoCountry = ReadString(root, "country");
                city = ReadString(root, "city");
                if (root.TryGetProperty("connection", out var connection))
                {
                    geoIsp = ReadString(connection, "isp");
                    var asnNumber = ReadString(connection, "asn");
                    var org = ReadString(connection, "org");
                    asn = string.IsNullOrWhiteSpace(org) ? asnNumber : $"{asnNumber} {org}".Trim();
                }
            }
        }
        catch
        {
            // Geo is optional when AbuseIPDB already answered.
        }

        return new IpReport
        {
            AbuseScore = score,
            Reports = reports,
            Usage = usage,
            Country = string.IsNullOrWhiteSpace(geoCountry) ? country : geoCountry,
            City = city,
            Isp = string.IsNullOrWhiteSpace(geoIsp) ? isp : geoIsp,
            Asn = asn
        };
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Number ? value.ToString() : null;

    private static string Trim(string body) => body.Length > 280 ? body[..280] : body;
}

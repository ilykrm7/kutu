using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using DnsClient;
using DnsClient.Protocol;
using Kutu.Core.Network;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Ui;
using Wpf.Ui.Controls;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace Kutu.Tools.Whois;

public sealed class WhoisTool : IKutuTool
{
    public string Id => "whois";

    public string TitleKey => "Tool_Whois_Title";

    public string DescriptionKey => "Tool_Whois_Desc";

    public string Symbol => "Globe24";

    public IReadOnlyList<string> Aliases { get; } = ["whois", "rdap", "dns", "domain", "alan", "ns"];

    public int Order => 70;

    public UIElement CreateView(IKutuHost host) => new WhoisView(host);
}

public sealed class WhoisView : UserControl
{
    private readonly TextBox _domain;
    private readonly TextBlock _status;
    private readonly StackPanel _result;
    private CancellationTokenSource? _pending;

    public WhoisView(IKutuHost host)
    {
        _domain = Chrome.Input("ornek.com");
        _domain.TextChanged += (_, _) => ScheduleLookup();
        _domain.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                _pending?.Cancel();
                await LookupAsync(explicitRequest: true);
            }
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        _domain.Width = 360;
        _domain.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(_domain);
        row.Children.Add(Chrome.Primary(L.Get("Action_Lookup"), SymbolRegular.Search24, async (_, _) =>
        {
            _pending?.Cancel();
            await LookupAsync(explicitRequest: true);
        }));

        _status = Chrome.Hint("");
        _result = new StackPanel();
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Label(L.Get("Whois_Domain")));
        stack.Children.Add(row);
        stack.Children.Add(_status);
        stack.Children.Add(_result);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private void ScheduleLookup()
    {
        _pending?.Cancel();
        var pending = new CancellationTokenSource();
        _pending = pending;
        _ = DebouncedLookupAsync(pending.Token);
    }

    private async Task DebouncedLookupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken);
            await LookupAsync(explicitRequest: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke replaced this query.
        }
    }

    private async Task LookupAsync(bool explicitRequest, CancellationToken cancellationToken = default)
    {
        var domain = DomainName.Normalize(_domain.Text);
        if (!DomainName.IsComplete(domain))
        {
            _result.Children.Clear();
            _status.Text = explicitRequest ? L.Get("Whois_Empty") : "";
            return;
        }

        _result.Children.Clear();
        _status.Text = L.Get("Common_Working");
        try
        {
            var summary = await RdapClient.LookupAsync(domain, cancellationToken);
            var dns = await DnsLookup.QueryAsync(domain, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;
            _status.Text = "";
            AddCard(
            [
                (L.Get("Whois_Domain"), string.IsNullOrWhiteSpace(summary.Domain) ? domain : summary.Domain),
                (L.Get("Whois_Registrar"), Blank(summary.Registrar)),
                (L.Get("Whois_Created"), Blank(summary.Registered)),
                (L.Get("Whois_Expires"), Blank(summary.Expires)),
                (L.Get("Whois_Updated"), Blank(summary.Updated)),
                (L.Get("Whois_Status"), summary.Status.Count == 0 ? L.Get("Common_None") : string.Join(", ", summary.Status)),
                (L.Get("Whois_Nameservers"), summary.NameServers.Count == 0 ? L.Get("Common_None") : string.Join("\n", summary.NameServers))
            ]);

            var dnsBody = new StackPanel();
            dnsBody.Children.Add(Chrome.Heading(L.Get("Whois_Dns")));
            if (dns.Count == 0)
            {
                dnsBody.Children.Add(Chrome.Hint(L.Get("Whois_Empty")));
            }
            else
            {
                foreach (var record in dns)
                {
                    dnsBody.Children.Add(new TextBlock
                    {
                        Text = $"{record.Type}   {record.Value}",
                        Foreground = Chrome.Text,
                        FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"),
                        Margin = new Thickness(0, 2, 0, 2),
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }

            _result.Children.Add(Chrome.Card(dnsBody));
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke replaced this query.
        }
        catch (RdapNotFoundException)
        {
            _status.Text = L.Get("Whois_Empty");
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
    }

    private void AddCard(IEnumerable<(string Label, string Value)> rows)
    {
        var panel = new StackPanel();
        foreach (var (label, value) in rows)
        {
            panel.Children.Add(Chrome.Label(label));
            panel.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = Chrome.Text,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 14
            });
        }

        _result.Children.Add(Chrome.Card(panel));
        _result.Children.Add(new Border { Height = 12 });
    }

    private static string Blank(string? value) => string.IsNullOrWhiteSpace(value) ? L.Get("Common_None") : value;
}

public static class RdapClient
{
    public static async Task<RdapSummary> LookupAsync(string domain, CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Kutu/1.0 (+https://github.com/ilykrm7/kutu)");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://rdap.org/domain/{Uri.EscapeDataString(domain)}");
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new RdapNotFoundException();
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return RdapParser.Parse(json);
    }
}

public static class DnsLookup
{
    public static async Task<IReadOnlyList<DnsRecord>> QueryAsync(string domain, CancellationToken cancellationToken = default)
    {
        var client = new LookupClient(new LookupClientOptions
        {
            Timeout = TimeSpan.FromSeconds(4),
            Retries = 1
        });
        var records = new List<DnsRecord>();
        foreach (var type in new[] { QueryType.A, QueryType.AAAA, QueryType.CNAME, QueryType.MX, QueryType.NS, QueryType.TXT })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await client.QueryAsync(domain, type, cancellationToken: cancellationToken);
            foreach (var answer in response.Answers)
            {
                records.Add(new DnsRecord(type.ToString(), Format(answer)));
            }
        }

        return records;
    }

    private static string Format(DnsResourceRecord record) => record switch
    {
        ARecord a => a.Address.ToString(),
        AaaaRecord aaaa => aaaa.Address.ToString(),
        CNameRecord cname => cname.CanonicalName.Value.TrimEnd('.'),
        MxRecord mx => $"{mx.Preference} {mx.Exchange.Value.TrimEnd('.')}",
        NsRecord ns => ns.NSDName.Value.TrimEnd('.'),
        TxtRecord txt => string.Concat(txt.Text),
        _ => record.ToString()
    };
}

public sealed class RdapNotFoundException : Exception
{
}

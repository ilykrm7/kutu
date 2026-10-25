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
        Chrome.BubbleWheel(_result);
        var stack = new StackPanel();
        stack.Children.Add(Chrome.Label(L.Get("Whois_Domain")));
        stack.Children.Add(row);
        stack.Children.Add(_status);
        stack.Children.Add(_result);
        Content = stack;
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
        var typed = _domain.Text;
        var domain = DomainName.Normalize(typed);
        if (!DomainName.IsComplete(domain))
        {
            if (!explicitRequest)
                return;

            _result.Children.Clear();
            _status.Text = L.Get("Whois_Empty");
            return;
        }

        if (explicitRequest)
            _status.Text = L.Get("Common_Working");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(14));
        var report = new DomainFetch();
        var collect = report.RunAsync(domain, limit.Token);
        var finished = await Task.WhenAny(collect, Task.Delay(TimeSpan.FromSeconds(14), cancellationToken));
        if (cancellationToken.IsCancellationRequested || !string.Equals(_domain.Text, typed, StringComparison.Ordinal))
            return;
        if (finished != collect)
            report.MarkTimeout();
        else
        {
            try
            {
                await collect;
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                report.MarkTimeout();
            }
        }

        if (!string.Equals(_domain.Text, typed, StringComparison.Ordinal))
            return;

        _result.Children.Clear();
        if (report.Rdap is not null)
        {
            var summary = report.Rdap;
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
        }

        var dnsBody = new StackPanel();
        dnsBody.Children.Add(Chrome.Heading(L.Get("Whois_Dns")));
        if (report.Dns.Count == 0)
        {
            var dnsFailed = report.DnsKind is "dns" or "timeout" or "failed" or "rdap";
            dnsBody.Children.Add(Chrome.Hint(dnsFailed ? L.Get("Whois_DnsFailed") : L.Get("Whois_Empty")));
        }
        else
        {
            foreach (var record in report.Dns)
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
        _status.Text = StatusText(report);
    }

    private static string StatusText(DomainFetch report)
    {
        var parts = new List<string>();
        if (report.Rdap is null && report.RdapKind == "empty")
            parts.Add(report.Dns.Count == 0 ? L.Get("Whois_Empty") : L.Get("Whois_RdapMissing"));
        else if (report.Rdap is null && report.RdapKind is not ("none" or "cancelled"))
            parts.Add(Friendly(report.RdapKind, rdap: true));
        if (report.DnsKind is "dns" or "timeout" or "failed")
            parts.Add(Friendly(report.DnsKind, rdap: false));
        if (report.TimedOut && parts.Count == 0)
            parts.Add(L.Get("Whois_Timeout"));
        var incomplete = report.TimedOut
            || report.RdapKind is "timeout" or "failed" or "rdap" or "dns"
            || report.DnsKind is "timeout" or "failed" or "dns" or "rdap";
        if (incomplete && (report.Rdap is not null || report.Dns.Count > 0))
            parts.Add(L.Get("Whois_Partial"));
        return string.Join(" ", parts);
    }

    private static string Friendly(string kind, bool rdap) => kind switch
    {
        "timeout" => L.Get("Whois_Timeout"),
        "dns" => L.Get("Whois_DnsFailed"),
        "rdap" => L.Get("Whois_RdapFailed"),
        "empty" => L.Get("Whois_Empty"),
        "cancelled" => "",
        _ => rdap ? L.Get("Whois_RdapFailed") : L.Get("Whois_Failed")
    };

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

public sealed class DomainFetch
{
    public RdapSummary? Rdap { get; private set; }

    public string RdapKind { get; private set; } = "none";

    public List<DnsRecord> Dns { get; } = [];

    public string DnsKind { get; private set; } = "none";

    public bool TimedOut { get; private set; }

    public void MarkTimeout()
    {
        TimedOut = true;
        if (Rdap is null && RdapKind is "none")
            RdapKind = "timeout";
        if (Dns.Count == 0 && DnsKind is "none")
            DnsKind = "timeout";
    }

    public async Task RunAsync(string domain, CancellationToken cancellationToken)
    {
        try
        {
            Rdap = await RdapClient.LookupAsync(domain, cancellationToken);
            RdapKind = "none";
        }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
                RdapKind = "timeout";
            else
                RdapKind = "cancelled";
        }
        catch (RdapNotFoundException)
        {
            RdapKind = "empty";
        }
        catch (Exception ex)
        {
            RdapKind = LookupFailure.Kind(ex);
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            var dns = await DnsLookup.QueryAsync(domain, cancellationToken);
            Dns.AddRange(dns.Records);
            DnsKind = dns.Partial ? "dns" : "none";
        }
        catch (OperationCanceledException)
        {
            DnsKind = cancellationToken.IsCancellationRequested ? "cancelled" : "timeout";
        }
        catch (Exception ex)
        {
            DnsKind = LookupFailure.Kind(ex);
            if (DnsKind == "cancelled")
                DnsKind = "dns";
        }
    }
}

public static class RdapClient
{
    public static async Task<RdapSummary> LookupAsync(string domain, CancellationToken cancellationToken = default)
    {
        TlsPolicy.Enable();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Kutu/1.1.5 (+https://github.com/ilykrm7/kutu)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json");
        Exception? last = null;
        var notFound = 0;
        foreach (var url in RdapUrls.For(domain))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limit.CancelAfter(TimeSpan.FromSeconds(7));
                var send = http.SendAsync(request, limit.Token);
                var finished = await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(7), cancellationToken));
                if (finished != send)
                    throw new TimeoutException();
                using var response = await send;
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    notFound++;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    last = new HttpRequestException(((int)response.StatusCode).ToString());
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();
                return RdapParser.Parse(json);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                last = new TimeoutException();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
            }
        }

        if (notFound > 0 && last is null)
            throw new RdapNotFoundException();
        throw last ?? new TimeoutException();
    }
}

public static class DnsLookup
{
    public static async Task<DnsQueryResult> QueryAsync(string domain, CancellationToken cancellationToken = default)
    {
        var servers = PublicDns.IPv4.Select(IPAddress.Parse).ToArray();
        var client = new LookupClient(new LookupClientOptions(servers)
        {
            Timeout = TimeSpan.FromSeconds(2),
            Retries = 0,
            UseCache = false,
            ThrowDnsErrors = false,
            UseRandomNameServer = false,
            ContinueOnDnsError = true
        });
        var records = new List<DnsRecord>();
        var failed = 0;
        foreach (var type in new[] { QueryType.A, QueryType.AAAA, QueryType.CNAME, QueryType.MX, QueryType.NS, QueryType.TXT })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var got = await QueryTypeAsync(client, domain, type, cancellationToken);
            if (got is null)
            {
                failed++;
                continue;
            }

            foreach (var answer in got.Answers)
                records.Add(new DnsRecord(type.ToString(), Format(answer)));
        }

        if (records.Count == 0 && failed > 0)
            throw new TimeoutException();
        return new DnsQueryResult(records, failed > 0);
    }

    private static async Task<IDnsQueryResponse?> QueryTypeAsync(LookupClient client, string domain, QueryType type, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var work = client.QueryAsync(domain, type, cancellationToken: limit.Token);
            var finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken));
            if (finished != work)
                return null;
            return await work;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
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

public readonly record struct DnsQueryResult(IReadOnlyList<DnsRecord> Records, bool Partial);

public sealed class RdapNotFoundException : Exception
{
}

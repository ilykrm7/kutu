using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using Kutu.Core.Models;
using Kutu.Core.Network;

namespace Kutu.Tools.IpReputation;

public sealed class ProviderOutcome
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string Status { get; set; } = "ok";

    public int? Score { get; set; }

    public bool? Tor { get; set; }

    public int? DnsblHits { get; set; }

    public string? Country { get; init; }

    public string? City { get; init; }

    public string? Isp { get; init; }

    public string? Asn { get; init; }

    public List<(string Label, string Value)> Lines { get; } = [];
}

public static class IpLookup
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static HashSet<string>? _tor;
    private static DateTime _torAt;

    static IpLookup() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("Kutu/1.0");

    public static async Task<string> PublicAddressAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(6));
        var text = (await HttpText.GetStringAsync(Http, "https://api.ipify.org", cts.Token)).Trim();
        if (!IPAddress.TryParse(text, out _))
            throw new InvalidOperationException("public-ip");
        return text;
    }

    public static Task<ProviderOutcome[]> CheckAllAsync(string ip, AppSettings settings, CancellationToken cancellationToken = default)
    {
        var work = new List<Task<ProviderOutcome>>
        {
            Guard("abuse", "AbuseIPDB", () => string.IsNullOrWhiteSpace(settings.AbuseIpDbKey) ? Task.FromResult(Missing("abuse", "AbuseIPDB")) : AbuseAsync(ip, settings.AbuseIpDbKey)),
            Guard("vt", "VirusTotal", () => string.IsNullOrWhiteSpace(settings.VirusTotalKey) ? Task.FromResult(Missing("vt", "VirusTotal")) : VirusTotalAsync(ip, settings.VirusTotalKey)),
            Guard("ipqs", "IPQualityScore", () => string.IsNullOrWhiteSpace(settings.IpQualityScoreKey) ? Task.FromResult(Missing("ipqs", "IPQualityScore")) : IpqsAsync(ip, settings.IpQualityScoreKey)),
            Guard("otx", "AlienVault OTX", () => string.IsNullOrWhiteSpace(settings.OtxKey) ? Task.FromResult(Missing("otx", "AlienVault OTX")) : OtxAsync(ip, settings.OtxKey)),
            Guard("grey", "GreyNoise", () => string.IsNullOrWhiteSpace(settings.GreyNoiseKey) ? Task.FromResult(Missing("grey", "GreyNoise")) : GreyAsync(ip, settings.GreyNoiseKey)),
            Guard("shodan", "Shodan", () => string.IsNullOrWhiteSpace(settings.ShodanKey) ? Task.FromResult(Missing("shodan", "Shodan")) : ShodanAsync(ip, settings.ShodanKey)),
            Guard("ipinfo", "IPinfo", () => string.IsNullOrWhiteSpace(settings.IpInfoKey) ? Task.FromResult(Missing("ipinfo", "IPinfo")) : IpInfoAsync(ip, settings.IpInfoKey)),
            Guard("geo", "Konum", () => GeoAsync(ip)),
            Guard("dnsbl", "Kara liste", () => DnsblAsync(ip)),
            Guard("tor", "TOR", () => TorAsync(ip))
        };
        return Task.WhenAll(work);
    }

    private static ProviderOutcome Missing(string id, string title) => new()
    {
        Id = id,
        Title = title,
        Status = "missing"
    };

    private static async Task<ProviderOutcome> Guard(string id, string title, Func<Task<ProviderOutcome>> work)
    {
        try
        {
            var task = work();
            var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(8)));
            if (done != task)
                return new ProviderOutcome { Id = id, Title = title, Status = "error", Lines = { ("", "Zaman aşımı") } };
            return await task;
        }
        catch (Exception ex)
        {
            return new ProviderOutcome { Id = id, Title = title, Status = "error", Lines = { ("", Trim(ex.Message)) } };
        }
    }

    private static async Task<ProviderOutcome> AbuseAsync(string ip, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.abuseipdb.com/api/v2/check?ipAddress={Uri.EscapeDataString(ip)}&maxAgeInDays=90");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Key", key);
        using var json = await SendAsync(request);
        var data = json.RootElement.GetProperty("data");
        var score = Int(data, "abuseConfidenceScore");
        var result = new ProviderOutcome
        {
            Id = "abuse",
            Title = "AbuseIPDB",
            Status = score is >= 25 ? "risk" : "clean",
            Score = score,
            Country = Str(data, "countryCode"),
            Isp = Str(data, "isp")
        };
        result.Lines.Add(("Skor", score?.ToString() ?? "—"));
        result.Lines.Add(("Rapor", Int(data, "totalReports")?.ToString() ?? "—"));
        result.Lines.Add(("Kullanım", Str(data, "usageType") ?? "—"));
        return result;
    }

    private static async Task<ProviderOutcome> VirusTotalAsync(string ip, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.virustotal.com/api/v3/ip_addresses/{Uri.EscapeDataString(ip)}");
        request.Headers.TryAddWithoutValidation("x-apikey", key);
        using var json = await SendAsync(request);
        var attr = json.RootElement.GetProperty("data").GetProperty("attributes");
        var stats = attr.GetProperty("last_analysis_stats");
        var malicious = Int(stats, "malicious") ?? 0;
        var suspicious = Int(stats, "suspicious") ?? 0;
        var score = Math.Min(100, (malicious * 12) + (suspicious * 4));
        var result = new ProviderOutcome
        {
            Id = "vt",
            Title = "VirusTotal",
            Status = score >= 25 ? "risk" : "clean",
            Score = score,
            Country = Str(attr, "country"),
            Isp = Str(attr, "as_owner")
        };
        result.Lines.Add(("Kötü amaçlı", malicious.ToString()));
        result.Lines.Add(("Şüpheli", suspicious.ToString()));
        return result;
    }

    private static async Task<ProviderOutcome> IpqsAsync(string ip, string key)
    {
        using var json = await GetJson($"https://ipqualityscore.com/api/json/ip/{Uri.EscapeDataString(key)}/{Uri.EscapeDataString(ip)}");
        var root = json.RootElement;
        var score = Int(root, "fraud_score");
        var result = new ProviderOutcome
        {
            Id = "ipqs",
            Title = "IPQualityScore",
            Status = score is >= 75 ? "risk" : "clean",
            Score = score,
            Country = Str(root, "country_code"),
            City = Str(root, "city"),
            Isp = Str(root, "ISP"),
            Asn = Str(root, "ASN"),
            Tor = Bool(root, "tor")
        };
        result.Lines.Add(("Dolandırıcılık", score?.ToString() ?? "—"));
        result.Lines.Add(("VPN", Yes(Bool(root, "vpn"))));
        result.Lines.Add(("Proxy", Yes(Bool(root, "proxy"))));
        result.Lines.Add(("TOR", Yes(Bool(root, "tor"))));
        return result;
    }

    private static async Task<ProviderOutcome> OtxAsync(string ip, string key)
    {
        var kind = ip.IndexOf(':') >= 0 ? "IPv6" : "IPv4";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://otx.alienvault.com/api/v1/indicators/{kind}/{Uri.EscapeDataString(ip)}/general");
        request.Headers.TryAddWithoutValidation("X-OTX-API-KEY", key);
        using var json = await SendAsync(request);
        var root = json.RootElement;
        var pulses = root.TryGetProperty("pulse_info", out var info) ? Int(info, "count") ?? 0 : 0;
        var score = Math.Min(100, pulses * 10);
        var result = new ProviderOutcome
        {
            Id = "otx",
            Title = "AlienVault OTX",
            Status = score >= 25 ? "risk" : "clean",
            Score = score,
            Country = Str(root, "country_name"),
            Asn = Str(root, "asn")
        };
        result.Lines.Add(("Pulse", pulses.ToString()));
        return result;
    }

    private static async Task<ProviderOutcome> GreyAsync(string ip, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.greynoise.io/v3/community/{Uri.EscapeDataString(ip)}");
        request.Headers.TryAddWithoutValidation("key", key);
        using var json = await SendAsync(request);
        var root = json.RootElement;
        var classification = Str(root, "classification") ?? "unknown";
        var score = classification.Equals("malicious", StringComparison.OrdinalIgnoreCase) ? 80
            : classification.Equals("benign", StringComparison.OrdinalIgnoreCase) ? 5 : 35;
        var result = new ProviderOutcome
        {
            Id = "grey",
            Title = "GreyNoise",
            Status = score >= 55 ? "risk" : "clean",
            Score = score
        };
        result.Lines.Add(("Sınıf", classification));
        result.Lines.Add(("Gürültü", Yes(Bool(root, "noise"))));
        if (Str(root, "name") is string name)
            result.Lines.Add(("Ad", name));
        return result;
    }

    private static async Task<ProviderOutcome> ShodanAsync(string ip, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.shodan.io/shodan/host/{Uri.EscapeDataString(ip)}?key={Uri.EscapeDataString(key)}");
        using var response = await Http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ProviderOutcome { Id = "shodan", Title = "Shodan", Status = "empty" };
        }

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(Trim(body));
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var ports = root.TryGetProperty("ports", out var portEl) && portEl.ValueKind == JsonValueKind.Array
            ? string.Join(", ", portEl.EnumerateArray().Take(12).Select(item => item.ToString()))
            : "—";
        var vulns = root.TryGetProperty("vulns", out var vulnEl) && vulnEl.ValueKind == JsonValueKind.Object ? vulnEl.EnumerateObject().Count() : 0;
        var result = new ProviderOutcome
        {
            Id = "shodan",
            Title = "Shodan",
            Status = vulns > 0 ? "risk" : "ok",
            Score = vulns > 0 ? Math.Min(100, vulns * 15) : null,
            Country = Str(root, "country_name"),
            City = Str(root, "city"),
            Isp = Str(root, "isp") ?? Str(root, "org")
        };
        result.Lines.Add(("Portlar", ports));
        result.Lines.Add(("Zafiyet", vulns.ToString()));
        if (Str(root, "os") is string os)
            result.Lines.Add(("OS", os));
        return result;
    }

    private static async Task<ProviderOutcome> IpInfoAsync(string ip, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://ipinfo.io/{Uri.EscapeDataString(ip)}/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var json = await SendAsync(request);
        var root = json.RootElement;
        var result = new ProviderOutcome
        {
            Id = "ipinfo",
            Title = "IPinfo",
            Status = "ok",
            Country = Str(root, "country"),
            City = Str(root, "city"),
            Isp = Str(root, "org")
        };
        if (Str(root, "hostname") is string host)
            result.Lines.Add(("Ana makine", host));
        if (Str(root, "region") is string region)
            result.Lines.Add(("Bölge", region));
        return result;
    }

    private static async Task<ProviderOutcome> GeoAsync(string ip)
    {
        using var json = await GetJson($"https://ipwho.is/{Uri.EscapeDataString(ip)}");
        var root = json.RootElement;
        if (root.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
            return new ProviderOutcome { Id = "geo", Title = "Konum", Status = "empty" };
        string? asn = null;
        string? isp = null;
        if (root.TryGetProperty("connection", out var connection))
        {
            isp = Str(connection, "isp");
            var number = Str(connection, "asn");
            var org = Str(connection, "org");
            asn = string.IsNullOrWhiteSpace(org) ? number : $"{number} {org}".Trim();
        }

        var result = new ProviderOutcome
        {
            Id = "geo",
            Title = "Konum",
            Status = "ok",
            Country = Str(root, "country"),
            City = Str(root, "city"),
            Isp = isp,
            Asn = asn
        };
        result.Lines.Add(("Ülke", result.Country ?? "—"));
        result.Lines.Add(("Şehir", result.City ?? "—"));
        result.Lines.Add(("ISS", result.Isp ?? "—"));
        result.Lines.Add(("ASN", result.Asn ?? "—"));
        return result;
    }

    private static async Task<ProviderOutcome> DnsblAsync(string ip)
    {
        var reverse = Dnsbl.Reverse(ip);
        var result = new ProviderOutcome { Id = "dnsbl", Title = "Kara liste", DnsblHits = 0 };
        if (reverse is null)
        {
            result.Status = "empty";
            return result;
        }

        var zones = new (string Name, string Zone)[]
        {
            ("Spamhaus ZEN", "zen.spamhaus.org"),
            ("SpamCop", "bl.spamcop.net"),
            ("Barracuda", "b.barracudacentral.org")
        };
        var hits = 0;
        foreach (var zone in zones)
        {
            var mark = await QueryZone(reverse + "." + zone.Zone);
            if (mark == DnsblMark.Listed)
                hits++;
            result.Lines.Add((zone.Name, mark switch
            {
                DnsblMark.Listed => "Listede",
                DnsblMark.Blocked => "Sorgu engellendi",
                _ => "Temiz"
            }));
        }

        result.DnsblHits = hits;
        result.Status = hits > 0 ? "risk" : "clean";
        result.Score = hits > 0 ? Math.Min(100, 36 + ((hits - 1) * 22)) : 0;
        return result;
    }

    private static async Task<DnsblMark> QueryZone(string host)
    {
        try
        {
            var lookup = Dns.GetHostAddressesAsync(host);
            var done = await Task.WhenAny(lookup, Task.Delay(4000));
            if (done != lookup)
                return DnsblMark.Blocked;
            var addresses = await lookup;
            return addresses.Select(Dnsbl.Classify).FirstOrDefault(mark => mark != DnsblMark.Clean);
        }
        catch (SocketException)
        {
            return DnsblMark.Clean;
        }
        catch
        {
            return DnsblMark.Blocked;
        }
    }

    private static async Task<ProviderOutcome> TorAsync(string ip)
    {
        if (_tor is null || DateTime.UtcNow - _torAt > TimeSpan.FromHours(1))
        {
            var text = await Http.GetStringAsync("https://check.torproject.org/torbulkexitlist");
            _tor = new HashSet<string>(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            _torAt = DateTime.UtcNow;
        }

        var listed = _tor.Contains(ip);
        var result = new ProviderOutcome
        {
            Id = "tor",
            Title = "TOR",
            Status = listed ? "risk" : "clean",
            Tor = listed,
            Score = listed ? 48 : 0
        };
        result.Lines.Add(("Çıkış", listed ? "Evet" : "Hayır"));
        return result;
    }

    private static async Task<JsonDocument> SendAsync(HttpRequestMessage request)
    {
        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Anahtar kabul edilmedi");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(Trim(body));
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument> GetJson(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await SendAsync(request);
    }

    private static string? Str(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string Yes(bool value) => value ? "Evet" : "Hayır";

    private static string Trim(string body) => string.IsNullOrWhiteSpace(body) ? "Ulaşılamadı" : body.Length > 160 ? body[..160] : body;
}

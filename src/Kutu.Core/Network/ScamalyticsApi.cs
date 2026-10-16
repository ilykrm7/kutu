using System.Text.Json;

namespace Kutu.Core.Network;

/// <summary>
/// Official Scamalytics v3 API on api12. The public website is not a lookup source.
/// </summary>
public static class ScamalyticsApi
{
    public sealed record Reading(bool Ok, int? Score, string? Risk, bool? Vpn, bool? Datacenter, string Error);

    public static bool HasCredentials(string? username, string? key) =>
        !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(key);

    public static string Url(string username, string key, string ip) =>
        "https://api12.scamalytics.com/v3/"
        + Uri.EscapeDataString(username.Trim())
        + "?key="
        + Uri.EscapeDataString(key.Trim())
        + "&ip="
        + Uri.EscapeDataString(ip.Trim());

    public static Reading Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("scamalytics", out var body)
            || body.ValueKind != JsonValueKind.Object)
            return Fail("Ulaşılamadı");

        var status = Text(body, "status");
        if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            var message = Text(body, "error") ?? "";
            if (message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("invalid api", StringComparison.OrdinalIgnoreCase) >= 0)
                return Fail("Anahtar kabul edilmedi");
            return Fail("Sorgu tamamlanamadı");
        }

        bool? vpn = null;
        bool? datacenter = null;
        if (body.TryGetProperty("scamalytics_proxy", out var proxy) && proxy.ValueKind == JsonValueKind.Object)
        {
            vpn = Flag(proxy, "is_vpn");
            datacenter = Flag(proxy, "is_datacenter");
        }

        return new Reading(true, Score(body), Text(body, "scamalytics_risk"), vpn, datacenter, "");
    }

    public static bool IsRisk(int? score, string? risk)
    {
        var band = (risk ?? "").Trim().ToLowerInvariant();
        if (band is "medium" or "high" or "very high")
            return true;
        return score is >= 20;
    }

    public static string RiskLabel(string? risk)
    {
        var band = (risk ?? "").Trim().ToLowerInvariant();
        return band switch
        {
            "low" => "düşük",
            "medium" => "orta",
            "high" => "yüksek",
            "very high" => "çok yüksek",
            "" => "—",
            _ => risk!.Trim()
        };
    }

    public static string Cell(int? score, string? risk) =>
        score is int value
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + RiskLabel(risk)
            : "";

    private static Reading Fail(string error) => new(false, null, null, null, null, error);

    private static int? Score(JsonElement body)
    {
        if (!body.TryGetProperty("scamalytics_score", out var value) || value.ValueKind != JsonValueKind.Number)
            return null;
        int parsed;
        if (value.TryGetInt32(out var whole))
            parsed = whole;
        else if (value.TryGetDouble(out var number) && !double.IsNaN(number) && !double.IsInfinity(number))
            parsed = (int)Math.Round(number);
        else
            return null;
        if (parsed < 0)
            return 0;
        return parsed > 100 ? 100 : parsed;
    }

    private static string? Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString() ?? "";
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool? Flag(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}

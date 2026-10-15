namespace Kutu.Core.Network;

/// <summary>
/// Broadcom says Site Review has no public API (knowledge article 175706).
/// WebPulse reputation queries belong to licensed Security Analytics, not an open endpoint.
/// Automated use of sitereview.bluecoat.com violates the site's acceptable-use rules,
/// so Kutu only opens the lookup page in the user's browser and never invents a score.
/// </summary>
public static class SymantecReview
{
    public const string Finding =
        "No public Site Review or WebPulse API. Broadcom article 175706: Site Review has no public-facing API. " +
        "WebPulse enrichment is a licensed Security Analytics feature. Automated lookups violate Site Review acceptable use. " +
        "Kutu opens the lookup page per IP and does not invent results.";

    public static string Url(string ip)
    {
        var value = (ip ?? "").Trim();
        return "https://sitereview.bluecoat.com/sitereview.jsp?url=" + Uri.EscapeDataString(value);
    }
}

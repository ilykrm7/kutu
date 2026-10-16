namespace Kutu.Core.Network;

public static class PublicDns
{
    public static readonly string[] IPv4 = ["1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4"];
}

public static class RdapUrls
{
    public static IReadOnlyList<string> For(string domain)
    {
        var name = DomainName.Normalize(domain);
        var urls = new List<string> { "https://rdap.org/domain/" + name };
        var dot = name.LastIndexOf('.');
        var tld = dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : "";
        if (tld is "com" or "net")
            urls.Add("https://rdap.verisign.com/com/v1/domain/" + name);
        urls.Add("https://www.rdap.net/domain/" + name);
        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public static class LookupFailure
{
    public static string Kind(Exception? error)
    {
        if (error is null)
            return "none";
        if (error is OperationCanceledException && error is not TimeoutException)
            return "cancelled";

        var message = error.Message ?? "";
        var dns = message.IndexOf(" IN ", StringComparison.Ordinal) >= 0
            || error.GetType().Name.IndexOf("Dns", StringComparison.OrdinalIgnoreCase) >= 0;
        var timeout = error is TimeoutException
            || message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        if (dns)
            return "dns";
        if (timeout)
            return "timeout";
        if (error.GetType().Name.IndexOf("Http", StringComparison.OrdinalIgnoreCase) >= 0)
            return "rdap";
        return "failed";
    }
}

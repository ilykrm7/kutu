using System.Globalization;

namespace Kutu.Core.Network;

public static class CertificateText
{
    public static string Protocol(string? name)
    {
        var protocol = name ?? "";
        if (string.IsNullOrWhiteSpace(protocol))
            return "—";

        if (protocol.IndexOf("Tls13", StringComparison.Ordinal) >= 0)
            return "TLS 1.3";
        if (protocol.IndexOf("Tls12", StringComparison.Ordinal) >= 0)
            return "TLS 1.2";
        if (protocol.IndexOf("Tls11", StringComparison.Ordinal) >= 0)
            return "TLS 1.1";
        if (protocol.IndexOf("Tls", StringComparison.Ordinal) >= 0)
            return "TLS 1.0";
        if (protocol.IndexOf("Ssl3", StringComparison.Ordinal) >= 0)
            return "SSL 3";
        if (protocol.IndexOf("Ssl2", StringComparison.Ordinal) >= 0)
            return "SSL 2";
        return protocol;
    }

    public static string Stamp(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        return local.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }
}

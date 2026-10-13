namespace Kutu.Core.Network;

public static class DomainName
{
    public static string Normalize(string input)
    {
        var text = input.Trim();
        if (text.Contains("://", StringComparison.Ordinal))
            text = text.Split("://", 2)[1];

        var slash = text.IndexOf('/');
        if (slash >= 0)
            text = text[..slash];

        var query = text.IndexOf('?');
        if (query >= 0)
            text = text[..query];

        return text.Trim().TrimEnd('.').ToLowerInvariant();
    }
}

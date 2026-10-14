namespace Kutu.Core.Network;

public static class DomainName
{
    public static string Normalize(string input)
    {
        var text = input.Trim();
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            text = text.Substring(scheme + 3);

        var slash = text.IndexOf('/');
        if (slash >= 0)
            text = text[..slash];

        var query = text.IndexOf('?');
        if (query >= 0)
            text = text[..query];

        return text.Trim().TrimEnd('.').ToLowerInvariant();
    }

    public static bool IsComplete(string input)
    {
        var domain = Normalize(input);
        if (domain.Length == 0 || domain.IndexOf(' ') >= 0 || domain.IndexOf(':') >= 0)
            return false;

        var labels = domain.Split('.');
        if (labels.Length < 2)
            return false;

        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63)
                return false;
            if (label[0] == '-' || label[^1] == '-')
                return false;
            foreach (var ch in label)
            {
                if (ch is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                    return false;
            }
        }

        var tld = labels[^1];
        if (tld.Length < 2)
            return false;
        foreach (var ch in tld)
        {
            if (ch is < 'a' or > 'z')
                return false;
        }

        return true;
    }
}

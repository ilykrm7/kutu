using System.Net;
using System.Net.Sockets;

namespace Kutu.Core.Network;

public static class IpList
{
    public static IReadOnlyList<string> Parse(string? text)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
            return found;

        var token = new System.Text.StringBuilder();
        void Flush()
        {
            if (token.Length == 0)
                return;
            var value = token.ToString();
            token.Clear();
            if (!IPAddress.TryParse(value, out var address))
                return;
            if (address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
                return;
            var key = address.ToString();
            if (seen.Add(key))
                found.Add(key);
        }

        foreach (var ch in text)
        {
            if (ch is ' ' or '\t' or '\r' or '\n' or ',' or ';' or '|')
                Flush();
            else
                token.Append(ch);
        }

        Flush();
        return found;
    }
}

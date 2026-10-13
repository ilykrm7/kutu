namespace Kutu.Core.Network;

public static class IpRisk
{
    public static int? Combine(IReadOnlyList<int?> scores, int? dnsblHits, bool? torExit)
    {
        var values = new List<int>();
        foreach (var score in scores)
        {
            if (score is int value)
                values.Add(Math.Clamp(value, 0, 100));
        }

        if (dnsblHits is > 0)
            values.Add(Math.Min(100, 36 + ((dnsblHits.Value - 1) * 22)));
        if (torExit == true)
            values.Add(48);
        if (values.Count == 0)
            return dnsblHits == 0 && torExit == false ? 6 : null;

        var max = values.Max();
        var average = values.Average();
        return (int)Math.Round((max * 0.7) + (average * 0.3));
    }

    public static string Level(int? score) => score switch
    {
        null => "info",
        < 20 => "low",
        < 55 => "medium",
        _ => "high"
    };
}

public enum DnsblMark
{
    Clean,
    Listed,
    Blocked
}

public static class Dnsbl
{
    public static string? Reverse(string ip)
    {
        if (!System.Net.IPAddress.TryParse(ip, out var address))
            return null;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return $"{bytes[3]}.{bytes[2]}.{bytes[1]}.{bytes[0]}";

        var chars = new char[(bytes.Length * 4) - 1];
        var index = 0;
        for (var i = bytes.Length - 1; i >= 0; i--)
        {
            chars[index++] = Hex(bytes[i] & 0x0F);
            chars[index++] = '.';
            chars[index++] = Hex(bytes[i] >> 4);
            if (i > 0)
                chars[index++] = '.';
        }

        return new string(chars);
    }

    public static DnsblMark Classify(System.Net.IPAddress? address)
    {
        if (address is null || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return DnsblMark.Clean;

        var bytes = address.GetAddressBytes();
        if (bytes[0] != 127)
            return DnsblMark.Clean;
        if (bytes[1] == 255 && bytes[2] == 255)
            return DnsblMark.Blocked;
        return DnsblMark.Listed;
    }

    private static char Hex(int value) => (char)(value < 10 ? '0' + value : 'a' + (value - 10));
}

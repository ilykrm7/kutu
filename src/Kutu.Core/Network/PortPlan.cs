namespace Kutu.Core.Network;

public static class PortPlan
{
    public static readonly int[] Priority = [3389, 22];

    public static readonly int[] Common =
    [
        21, 22, 23, 25, 53, 80, 110, 135, 139, 143, 443, 445,
        993, 995, 1433, 1521, 3306, 3389, 5432, 5900, 6379, 8080, 8443, 27017
    ];

    public static bool TryBuild(string preset, string text, out int[] ports, out string error)
    {
        ports = [];
        error = "";
        IEnumerable<int> raw;
        var mode = (preset ?? "").Trim().ToLowerInvariant();
        if (mode is "all" or "tüm" or "tum")
        {
            raw = Enumerable.Range(1, 65535);
        }
        else if (mode is "common" or "sık" or "sik")
        {
            raw = Common;
        }
        else if (mode is "single" or "tek")
        {
            if (!TryPort(text, out var port))
            {
                error = "invalid";
                return false;
            }

            raw = [port];
        }
        else
        {
            if (!TryRange(text, out var start, out var end))
            {
                error = "invalid";
                return false;
            }

            raw = Enumerable.Range(start, end - start + 1);
        }

        ports = WithPriority(raw);
        if (ports.Length == 0)
        {
            error = "empty";
            return false;
        }

        return true;
    }

    public static int[] WithPriority(IEnumerable<int> ports)
    {
        var seen = new HashSet<int>();
        var rest = new List<int>();
        var wanted = new HashSet<int>();
        foreach (var port in ports)
        {
            if (port is < 1 or > 65535 || !seen.Add(port))
                continue;
            wanted.Add(port);
            if (port is not 3389 and not 22)
                rest.Add(port);
        }

        var ordered = new List<int>(seen.Count);
        foreach (var first in Priority)
        {
            if (wanted.Contains(first))
                ordered.Add(first);
        }

        ordered.AddRange(rest);
        return ordered.ToArray();
    }

    public static bool TryPort(string? text, out int port)
    {
        port = 0;
        return int.TryParse((text ?? "").Trim(), out port) && port is >= 1 and <= 65535;
    }

    public static bool TryRange(string? text, out int start, out int end)
    {
        start = 0;
        end = 0;
        var value = (text ?? "").Trim();
        var dash = value.IndexOf('-');
        if (dash <= 0 || dash == value.Length - 1)
            return TryPort(value, out start) && (end = start) >= 1;

        if (!TryPort(value[..dash], out start) || !TryPort(value[(dash + 1)..], out end))
            return false;
        if (start > end)
            (start, end) = (end, start);
        return true;
    }
}

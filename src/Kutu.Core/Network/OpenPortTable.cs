namespace Kutu.Core.Network;

public sealed class OpenPortTable
{
    private readonly List<int> _ports = [];
    private readonly HashSet<int> _seen = [];

    public int Count => _ports.Count;

    public IReadOnlyList<int> Ports => _ports;

    public bool Add(int port)
    {
        if (port is < 1 or > 65535 || !_seen.Add(port))
            return false;

        _ports.Add(port);
        return true;
    }
}

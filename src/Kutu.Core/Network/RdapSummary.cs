namespace Kutu.Core.Network;

public sealed class RdapSummary
{
    public string Domain { get; set; } = "";

    public string Registrar { get; set; } = "";

    public string? Registered { get; set; }

    public string? Expires { get; set; }

    public string? Updated { get; set; }

    public List<string> Status { get; set; } = [];

    public List<string> NameServers { get; set; } = [];
}

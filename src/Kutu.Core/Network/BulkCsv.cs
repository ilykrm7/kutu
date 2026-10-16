using System.Text;

namespace Kutu.Core.Network;

public sealed record BulkIpExport(
    string Ip,
    string Risk,
    string Country,
    string City,
    string Isp,
    string Asn,
    string Dnsbl,
    string Tor,
    string ScamalyticsScore,
    string ScamalyticsRisk,
    string SymantecUrl);

public static class BulkCsv
{
    public static string Write(IEnumerable<BulkIpExport> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ip,risk,country,city,isp,asn,dnsbl,tor,scamalytics_score,scamalytics_risk,symantec_url");
        foreach (var row in rows)
        {
            builder.Append(Cell(row.Ip)).Append(',')
                .Append(Cell(row.Risk)).Append(',')
                .Append(Cell(row.Country)).Append(',')
                .Append(Cell(row.City)).Append(',')
                .Append(Cell(row.Isp)).Append(',')
                .Append(Cell(row.Asn)).Append(',')
                .Append(Cell(row.Dnsbl)).Append(',')
                .Append(Cell(row.Tor)).Append(',')
                .Append(Cell(row.ScamalyticsScore)).Append(',')
                .Append(Cell(row.ScamalyticsRisk)).Append(',')
                .Append(Cell(row.SymantecUrl)).AppendLine();
        }

        return builder.ToString();
    }

    public static string Cell(string? value)
    {
        var text = value ?? "";
        if (text.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}

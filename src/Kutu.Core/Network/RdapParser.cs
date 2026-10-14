using System.Text.Json;

namespace Kutu.Core.Network;

public static class RdapParser
{
    public static RdapSummary Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var summary = new RdapSummary();

        if (root.TryGetProperty("ldhName", out var ldh))
            summary.Domain = ldh.GetString() ?? "";
        else if (root.TryGetProperty("unicodeName", out var unicode))
            summary.Domain = unicode.GetString() ?? "";

        if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in status.EnumerateArray())
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    summary.Status.Add(value);
            }
        }

        if (root.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
        {
            foreach (var ev in events.EnumerateArray())
            {
                var action = ev.TryGetProperty("eventAction", out var actionEl) ? actionEl.GetString() : null;
                var date = ev.TryGetProperty("eventDate", out var dateEl) ? dateEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(date))
                    continue;

                switch (action)
                {
                    case "registration":
                        summary.Registered ??= date;
                        break;
                    case "expiration":
                        summary.Expires ??= date;
                        break;
                    case "last changed":
                    case "last update of RDAP database":
                        if (action == "last changed")
                            summary.Updated = date;
                        break;
                }
            }
        }

        if (root.TryGetProperty("nameservers", out var servers) && servers.ValueKind == JsonValueKind.Array)
        {
            foreach (var server in servers.EnumerateArray())
            {
                string? name = null;
                if (server.TryGetProperty("ldhName", out var nameEl))
                    name = nameEl.GetString();
                else if (server.TryGetProperty("unicodeName", out var unicodeEl))
                    name = unicodeEl.GetString();

                if (!string.IsNullOrWhiteSpace(name))
                    summary.NameServers.Add(name);
            }
        }

        summary.Registrar = FindRegistrar(root);
        return summary;
    }

    private static string FindRegistrar(JsonElement root)
    {
        if (!root.TryGetProperty("entities", out var entities) || entities.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var entity in entities.EnumerateArray())
        {
            if (!HasRole(entity, "registrar"))
                continue;

            var name = ReadVcardName(entity);
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            if (entity.TryGetProperty("handle", out var handle))
                return handle.GetString() ?? "";
        }

        return "";
    }

    private static bool HasRole(JsonElement entity, string role)
    {
        if (!entity.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in roles.EnumerateArray())
        {
            if (string.Equals(item.GetString(), role, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ReadVcardName(JsonElement entity)
    {
        if (!entity.TryGetProperty("vcardArray", out var vcard) || vcard.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var entry in vcard.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var row in entry.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 4)
                    continue;

                var kind = row[0].GetString();
                if (kind is not ("fn" or "org"))
                    continue;

                var value = row[3];
                if (value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? "";

                if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 && value[0].ValueKind == JsonValueKind.String)
                    return value[0].GetString() ?? "";
            }
        }

        return "";
    }
}

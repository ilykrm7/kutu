using System.Text.Json;

namespace Kutu.Core.Updates;

public sealed record ReleaseAsset(string Name, string Url);

public sealed record RemoteRelease(string Version, IReadOnlyList<ReleaseAsset> Assets);

public static class ReleaseVersion
{
    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var token = text.Trim();
        if (token.Length > 0 && (token[0] == 'v' || token[0] == 'V'))
            token = token[1..];
        var end = 0;
        while (end < token.Length && (char.IsDigit(token[end]) || token[end] == '.'))
            end++;
        token = token[..end].Trim('.');
        var parts = token.Split('.');
        if (parts.Length == 0 || parts.Length > 4)
            return false;
        if (parts.Length < 2)
            token += ".0";
        return Version.TryParse(token, out version!);
    }

    public static bool IsNewer(string? remote, Version local)
    {
        if (!TryParse(remote, out var version))
            return false;
        var current = new Version(local.Major, local.Minor, Math.Max(local.Build, 0));
        var other = new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        return other > current;
    }

    public static RemoteRelease? ParseLatest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var version = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
        if (string.IsNullOrWhiteSpace(version))
            return null;

        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in list.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
                    assets.Add(new ReleaseAsset(name, url));
            }
        }

        return new RemoteRelease(version, assets);
    }

    public static ReleaseAsset? Find(IEnumerable<ReleaseAsset> assets, string name) =>
        assets.FirstOrDefault(asset => asset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

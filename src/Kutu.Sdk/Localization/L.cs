using System.Globalization;
using System.Resources;

namespace Kutu.Sdk.Localization;

public static class L
{
    private static readonly ResourceManager ResourceManager = new("Kutu.Sdk.Resources.Strings", typeof(L).Assembly);

    public static CultureInfo Culture { get; private set; } = new("tr");

    public static void SetCulture(string? language)
    {
        var culture = language is not null && language.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("en")
            : new CultureInfo("tr");
        Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        return ResourceManager.GetString(key, Culture) ?? key;
    }
}

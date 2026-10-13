using System.Net.Http.Json;
using System.Text.Json;

namespace Kutu.Tools.Translate;

public sealed record TranslationResult(string Text, bool UsedFallback);

public static class TranslateClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<TranslationResult> TranslateAsync(
        string text,
        string source,
        string target,
        string? deepLKey,
        string deepLEndpoint,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(deepLKey))
        {
            var translated = await DeepLAsync(text, source, target, deepLKey, deepLEndpoint, cancellationToken);
            return new TranslationResult(translated, false);
        }

        var fallback = await MyMemoryAsync(text, source, target, cancellationToken);
        return new TranslationResult(fallback, true);
    }

    private static async Task<string> DeepLAsync(string text, string source, string target, string key, string endpoint, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["text"] = new[] { text },
            ["target_lang"] = target.ToUpperInvariant()
        };
        if (!source.Equals("auto", StringComparison.OrdinalIgnoreCase))
            payload["source_lang"] = source.ToUpperInvariant();

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + key);
        using var response = await Http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(body.Length > 300 ? body[..300] : body);

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? "";
    }

    private static async Task<string> MyMemoryAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var from = source.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "autodetect" : source.ToLowerInvariant();
        var to = target.ToLowerInvariant();
        var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(from)}|{Uri.EscapeDataString(to)}";
        var body = await Http.GetStringAsync(url, cancellationToken);
        using var json = JsonDocument.Parse(body);
        var translated = json.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString() ?? "";
        if (translated.Contains("QUERY LENGTH LIMIT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(translated);
        return translated;
    }
}

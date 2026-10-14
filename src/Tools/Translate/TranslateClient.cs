using System.Net.Http.Json;
using System.Text.Json;
using Kutu.Core.Network;

namespace Kutu.Tools.Translate;

public sealed record TranslationResult(string Text, bool UsedFallback);

public static class TranslateClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<TranslationResult> TranslateAsync(
        string text,
        string source,
        string target,
        string? deepLKey,
        string deepLEndpoint,
        CancellationToken cancellationToken = default)
    {
        var normalizedTarget = string.IsNullOrWhiteSpace(target) ? "EN" : target;
        if (!string.IsNullOrWhiteSpace(deepLKey))
        {
            try
            {
                var translated = await DeepLAsync(text, source, normalizedTarget, deepLKey, deepLEndpoint, cancellationToken);
                return new TranslationResult(string.IsNullOrWhiteSpace(translated) ? text : translated, false);
            }
            catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
            {
                return new TranslationResult(text, false);
            }
            catch
            {
                // DeepL failed; try the free endpoints.
            }
        }

        try
        {
            var google = await GoogleAsync(text, normalizedTarget, cancellationToken);
            if (!string.IsNullOrWhiteSpace(google))
                return new TranslationResult(google, true);
        }
        catch
        {
            // Google is optional.
        }

        try
        {
            var memory = await MyMemoryAsync(text, source, normalizedTarget, cancellationToken);
            if (IsSameLanguage(memory))
                return new TranslationResult(text, true);
            return new TranslationResult(memory, true);
        }
        catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
        {
            return new TranslationResult(text, true);
        }
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
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            if (IsSameLanguage(body))
                return text;
            throw new InvalidOperationException(body.Length > 300 ? body[..300] : body);
        }

        using var json = JsonDocument.Parse(body);
        var translation = json.RootElement.GetProperty("translations")[0];
        var detected = translation.TryGetProperty("detected_source_language", out var lang) ? lang.GetString() : null;
        if (detected is not null && detected.Equals(target, StringComparison.OrdinalIgnoreCase))
            return text;
        return translation.GetProperty("text").GetString() ?? text;
    }

    private static async Task<string> GoogleAsync(string text, string target, CancellationToken cancellationToken)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl="
            + Uri.EscapeDataString(target.ToLowerInvariant())
            + "&dt=t&q=" + Uri.EscapeDataString(text);
        var body = await HttpText.GetStringAsync(Http, url, cancellationToken);
        using var json = JsonDocument.Parse(body);
        var detected = json.RootElement.GetArrayLength() > 2 && json.RootElement[2].ValueKind == JsonValueKind.String
            ? json.RootElement[2].GetString()
            : null;
        if (detected is not null && detected.Equals(target, StringComparison.OrdinalIgnoreCase))
            return text;
        var parts = json.RootElement[0];
        var builder = new System.Text.StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.GetArrayLength() > 0 && part[0].ValueKind == JsonValueKind.String)
                builder.Append(part[0].GetString());
        }

        return builder.ToString();
    }

    private static async Task<string> MyMemoryAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var from = source.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "autodetect" : source.ToLowerInvariant();
        var to = target.ToLowerInvariant();
        var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(from)}|{Uri.EscapeDataString(to)}";
        var body = await HttpText.GetStringAsync(Http, url, cancellationToken);
        using var json = JsonDocument.Parse(body);
        var translated = json.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString() ?? "";
        if (IsSameLanguage(translated))
            return text;
        if (translated.IndexOf("QUERY LENGTH LIMIT", StringComparison.OrdinalIgnoreCase) >= 0)
            throw new InvalidOperationException(translated);
        return translated;
    }

    private static bool IsSameLanguage(string text) =>
        text.IndexOf("DISTINCT", StringComparison.OrdinalIgnoreCase) >= 0
        || text.IndexOf("same language", StringComparison.OrdinalIgnoreCase) >= 0
        || text.IndexOf("source and target", StringComparison.OrdinalIgnoreCase) >= 0;
}

using System.Net.Http.Json;
using System.Text.Json;
using Kutu.Core.Network;
using Kutu.Core.Text;

namespace Kutu.Tools.Translate;

public sealed record TranslationResult(string Text, bool UsedFallback, string? DetectedLanguage = null, string TargetLanguage = "");

public static class TranslateClient
{
    private static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        TlsPolicy.Enable();
        return new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public static Task<TranslationResult> TranslateAutoAsync(
        string text,
        string? deepLKey,
        string deepLEndpoint,
        CancellationToken cancellationToken = default)
    {
        var target = TranslateDirection.InitialTarget(text);
        return TranslateAutoCoreAsync(text, target, deepLKey, deepLEndpoint, cancellationToken);
    }

    private static async Task<TranslationResult> TranslateAutoCoreAsync(
        string text,
        string target,
        string? deepLKey,
        string deepLEndpoint,
        CancellationToken cancellationToken)
    {
        var first = await TranslateAsync(text, "auto", target, deepLKey, deepLEndpoint, cancellationToken);
        var retry = TranslateDirection.RetryTarget(target, first.DetectedLanguage);
        if (retry is null)
            return first;
        return await TranslateAsync(text, "auto", retry, deepLKey, deepLEndpoint, cancellationToken);
    }

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
                var attempt = await DeepLAsync(text, source, normalizedTarget, deepLKey, deepLEndpoint, cancellationToken);
                return Finish(text, attempt, normalizedTarget, false);
            }
            catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
            {
                return new TranslationResult(text, false, TranslateDirection.NormalizeLang(normalizedTarget), normalizedTarget);
            }
            catch
            {
                // DeepL failed; try the free endpoints.
            }
        }

        try
        {
            var google = await GoogleAsync(text, normalizedTarget, cancellationToken);
            if (!string.IsNullOrWhiteSpace(google.Text) || !string.IsNullOrWhiteSpace(google.Detected))
                return Finish(text, google, normalizedTarget, true);
        }
        catch
        {
            // Google is optional.
        }

        try
        {
            var memory = await MyMemoryAsync(text, source, normalizedTarget, cancellationToken);
            return Finish(text, memory, normalizedTarget, true);
        }
        catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
        {
            return new TranslationResult(text, true, null, normalizedTarget);
        }
    }

    private readonly record struct Attempt(string Text, string? Detected);

    private static TranslationResult Finish(string original, Attempt attempt, string target, bool usedFallback)
    {
        var detected = TranslateDirection.NormalizeLang(attempt.Detected);
        var same = detected.Length > 0 && detected.Equals(TranslateDirection.NormalizeLang(target), StringComparison.OrdinalIgnoreCase);
        var text = same || string.IsNullOrWhiteSpace(attempt.Text) ? original : attempt.Text;
        if (IsSameLanguage(text))
            text = original;
        return new TranslationResult(text, usedFallback, detected.Length == 0 ? null : detected, target);
    }

    private static async Task<Attempt> DeepLAsync(string text, string source, string target, string key, string endpoint, CancellationToken cancellationToken)
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
                return new Attempt(text, target);
            throw new InvalidOperationException(body.Length > 300 ? body[..300] : body);
        }

        using var json = JsonDocument.Parse(body);
        var translation = json.RootElement.GetProperty("translations")[0];
        var detected = translation.TryGetProperty("detected_source_language", out var lang) ? lang.GetString() : null;
        var translated = translation.GetProperty("text").GetString() ?? text;
        return new Attempt(translated, detected);
    }

    private static async Task<Attempt> GoogleAsync(string text, string target, CancellationToken cancellationToken)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl="
            + Uri.EscapeDataString(target.ToLowerInvariant())
            + "&dt=t&q=" + Uri.EscapeDataString(text);
        var body = await HttpText.GetStringAsync(Http, url, cancellationToken);
        using var json = JsonDocument.Parse(body);
        var detected = json.RootElement.GetArrayLength() > 2 && json.RootElement[2].ValueKind == JsonValueKind.String
            ? json.RootElement[2].GetString()
            : null;
        var parts = json.RootElement[0];
        var builder = new System.Text.StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.GetArrayLength() > 0 && part[0].ValueKind == JsonValueKind.String)
                builder.Append(part[0].GetString());
        }

        return new Attempt(builder.ToString(), detected);
    }

    private static async Task<Attempt> MyMemoryAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var from = source.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "autodetect" : source.ToLowerInvariant();
        var to = target.ToLowerInvariant();
        var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(from)}|{Uri.EscapeDataString(to)}";
        var body = await HttpText.GetStringAsync(Http, url, cancellationToken);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("responseData");
        var translated = data.GetProperty("translatedText").GetString() ?? "";
        var detected = data.TryGetProperty("detectedLanguage", out var lang) && lang.ValueKind == JsonValueKind.String
            ? lang.GetString()
            : null;
        if (translated.IndexOf("QUERY LENGTH LIMIT", StringComparison.OrdinalIgnoreCase) >= 0)
            throw new InvalidOperationException(translated);
        if (IsSameLanguage(translated))
            return new Attempt(text, detected ?? target);
        return new Attempt(translated, detected);
    }

    private static bool IsSameLanguage(string text) =>
        text.IndexOf("DISTINCT", StringComparison.OrdinalIgnoreCase) >= 0
        || text.IndexOf("same language", StringComparison.OrdinalIgnoreCase) >= 0
        || text.IndexOf("source and target", StringComparison.OrdinalIgnoreCase) >= 0;
}

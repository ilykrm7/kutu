using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kutu.Core.Diagnostics;
using Kutu.Core.Network;
using Kutu.Core.Text;

namespace Kutu.Tools.Translate;

public sealed record TranslationResult(string Text, bool UsedFallback, string? DetectedLanguage = null, string TargetLanguage = "", string Provider = "");

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
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult("", false, null, normalizedTarget);

        try
        {
            var google = await GoogleAllAsync(text, normalizedTarget, cancellationToken);
            AppLog.Write("translate provider=Google chars=" + text.Length);
            return Finish(text, google, normalizedTarget, false, "Google");
        }
        catch
        {
            // Google did not return an ordered translation. Try DeepL next.
        }

        if (!string.IsNullOrWhiteSpace(deepLKey))
        {
            try
            {
                var attempt = await DeepLAllAsync(text, source, normalizedTarget, deepLKey, deepLEndpoint, cancellationToken);
                AppLog.Write("translate provider=DeepL chars=" + text.Length);
                return Finish(text, attempt, normalizedTarget, true, "DeepL");
            }
            catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
            {
                AppLog.Write("translate provider=DeepL chars=" + text.Length);
                return new TranslationResult(text, true, TranslateDirection.NormalizeLang(normalizedTarget), normalizedTarget, "DeepL");
            }
            catch
            {
                // DeepL failed. MyMemory is only a short-text last resort.
            }
        }

        if (TranslationPlan.AllowMyMemory(text))
        {
            try
            {
                var memory = await MyMemoryAsync(text, source, normalizedTarget, cancellationToken);
                AppLog.Write("translate provider=MyMemory chars=" + text.Length);
                return Finish(text, memory, normalizedTarget, true, "MyMemory");
            }
            catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
            {
                return new TranslationResult(text, true, null, normalizedTarget, "MyMemory");
            }
        }

        throw new InvalidOperationException("translate-failed");
    }

    private readonly record struct Attempt(string Text, string? Detected);

    private static TranslationResult Finish(string original, Attempt attempt, string target, bool usedFallback, string provider)
    {
        var detected = TranslateDirection.NormalizeLang(attempt.Detected);
        var same = detected.Length > 0 && detected.Equals(TranslateDirection.NormalizeLang(target), StringComparison.OrdinalIgnoreCase);
        var text = same || string.IsNullOrWhiteSpace(attempt.Text) ? original : attempt.Text;
        if (IsSameLanguage(text))
            text = original;
        return new TranslationResult(text, usedFallback, detected.Length == 0 ? null : detected, target, provider);
    }

    private static Task<Attempt> GoogleAllAsync(string text, string target, CancellationToken cancellationToken)
    {
        string? detected = null;
        return ReadCombinedAsync(text, TranslationPlan.GoogleChunkChars, async (chunk, token) =>
        {
            var attempt = await GooglePostAsync(chunk, target, token);
            if (string.IsNullOrWhiteSpace(detected) && !string.IsNullOrWhiteSpace(attempt.Detected))
                detected = attempt.Detected;
            if (string.IsNullOrWhiteSpace(attempt.Text))
                throw new InvalidOperationException("empty");
            return attempt.Text;
        }, () => detected, cancellationToken);
    }

    private static async Task<Attempt> GooglePostAsync(string text, string target, CancellationToken cancellationToken)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl="
            + Uri.EscapeDataString(target.ToLowerInvariant())
            + "&dt=t";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["q"] = text })
        };
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await ReadUtf8Async(response).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || !TranslationPlan.TryJoinGoogle(body, out var translated, out var detected))
            throw new InvalidOperationException("google");
        return new Attempt(translated, detected);
    }

    private static Task<Attempt> DeepLAllAsync(string text, string source, string target, string key, string endpoint, CancellationToken cancellationToken)
    {
        string? detected = null;
        return ReadCombinedAsync(text, TranslationPlan.DeepLChunkChars, async (chunk, token) =>
        {
            var attempt = await DeepLAsync(chunk, source, target, key, endpoint, token);
            if (string.IsNullOrWhiteSpace(detected) && !string.IsNullOrWhiteSpace(attempt.Detected))
                detected = attempt.Detected;
            return attempt.Text;
        }, () => detected, cancellationToken);
    }

    private static async Task<Attempt> ReadCombinedAsync(
        string text,
        int maxChars,
        Func<string, CancellationToken, Task<string>> translate,
        Func<string?> detected,
        CancellationToken cancellationToken)
    {
        var combined = await TranslationPlan.TranslateInOrderAsync(text, maxChars, translate, cancellationToken).ConfigureAwait(false);
        return new Attempt(combined, detected());
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
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await ReadUtf8Async(response).ConfigureAwait(false);
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

    private static async Task<Attempt> MyMemoryAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        var from = source.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "autodetect" : source.ToLowerInvariant();
        var to = target.ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mymemory.translated.net/get")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["q"] = text,
                ["langpair"] = from + "|" + to
            })
        };
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await ReadUtf8Async(response).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("mymemory");
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

    private static async Task<string> ReadUtf8Async(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        return Encoding.UTF8.GetString(bytes);
    }

    private static bool IsSameLanguage(string text) =>
        text.IndexOf("DISTINCT", StringComparison.OrdinalIgnoreCase) >= 0
        || text.IndexOf("same language", StringComparison.OrdinalIgnoreCase) >= 0
        || text.IndexOf("source and target", StringComparison.OrdinalIgnoreCase) >= 0;
}

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
        if (string.IsNullOrEmpty(text))
            return new TranslationResult("", false, null, normalizedTarget);

        var deepLConfigured = !string.IsNullOrWhiteSpace(deepLKey);
        string? detected = null;
        string provider = "";

        async Task<string> TranslateChunkAsync(string chunk, CancellationToken token)
        {
            foreach (var name in TranslationPlan.ProviderOrder(chunk, deepLConfigured))
            {
                try
                {
                    var attempt = name switch
                    {
                        "DeepL" => await DeepLAsync(chunk, source, normalizedTarget, deepLKey ?? "", deepLEndpoint, token).ConfigureAwait(false),
                        "MyMemory" => await MyMemoryAsync(chunk, source, normalizedTarget, token).ConfigureAwait(false),
                        _ => await GooglePostAsync(chunk, normalizedTarget, token).ConfigureAwait(false)
                    };
                    if (name == "Google" && string.IsNullOrWhiteSpace(attempt.Text))
                        continue;
                    if (string.IsNullOrWhiteSpace(detected) && !string.IsNullOrWhiteSpace(attempt.Detected))
                        detected = attempt.Detected;
                    provider = PreferProvider(provider, name);
                    AppLog.Write("translate provider=" + name + " chars=" + chunk.Length);
                    return OneLine(attempt.Text);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (InvalidOperationException ex) when (IsSameLanguage(ex.Message))
                {
                    provider = PreferProvider(provider, name);
                    return chunk;
                }
                catch
                {
                    // This provider missed the chunk. The next one in order tries the same text.
                }
            }

            if (chunk.Length <= TranslationPlan.MyMemoryMaxChars)
                throw new InvalidOperationException("translate-failed");

            return await TranslationPlan.TranslateInOrderAsync(chunk, TranslationPlan.MyMemoryMaxChars, TranslateChunkAsync, token).ConfigureAwait(false);
        }

        var combined = await TranslationPlan.TranslateInOrderAsync(text, TranslationPlan.GoogleChunkChars, TranslateChunkAsync, cancellationToken).ConfigureAwait(false);
        return Finish(text, new Attempt(combined, detected), normalizedTarget, provider != "Google", provider);
    }

    private static string PreferProvider(string current, string next)
    {
        if (next == "MyMemory" || current.Length == 0)
            return next;
        if (next == "DeepL" && current == "Google")
            return next;
        return current;
    }

    private static string OneLine(string translated)
    {
        if (string.IsNullOrEmpty(translated))
            return "";
        var start = 0;
        var end = translated.Length;
        while (start < end && (translated[start] == '\n' || translated[start] == '\r'))
            start++;
        while (end > start && (translated[end - 1] == '\n' || translated[end - 1] == '\r'))
            end--;
        var slice = translated.Substring(start, end - start);
        if (slice.IndexOf('\n') < 0 && slice.IndexOf('\r') < 0)
            return slice;
        return slice.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
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

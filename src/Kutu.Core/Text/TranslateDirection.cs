using System.Globalization;

namespace Kutu.Core.Text;

/// <summary>
/// Turkish goes to English. Anything else starts toward Turkish.
/// A local hint (Turkish letters or common words) wins before a provider.
/// If a provider was asked for Turkish and reports Turkish, the caller retries English.
/// </summary>
public static class TranslateDirection
{
    public static readonly string[] Targets = ["TR", "EN", "DE", "FR", "ES", "IT", "PT", "NL", "RU", "AR", "ZH", "JA", "KO"];

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly HashSet<string> TurkishWords = new(StringComparer.Ordinal)
    {
        "bir", "için", "değil", "olan", "olarak", "sonra", "önce", "kadar", "gibi", "daha",
        "çok", "ama", "veya", "çünkü", "diye", "şey", "zaman", "gün", "iyi", "kötü",
        "büyük", "küçük", "yeni", "eski", "merhaba", "selam", "lütfen", "evet", "hayır",
        "teşekkür", "teşekkürler", "nedir", "nasıl", "hiçbir", "şimdi", "ayrıca", "ancak",
        "fakat", "yani", "göre", "bugün", "yarın", "ederim", "vardır", "yoktur", "benim",
        "senin", "bizim", "sizin", "onların", "burada", "şurada", "orada", "nerede", "neden",
        "niçin", "hangi", "böyle", "şöyle", "öyle", "bence", "galiba", "herhalde", "gerçekten",
        "sadece", "bile", "artık", "henüz", "hâlâ", "işte", "tabii", "tamam", "peki",
        "olur", "olmaz", "istiyorum", "yapmak", "ediyor", "ediyorum", "gidiyor", "geliyor",
        "günaydın", "iyi akşamlar", "hoşça", "kolay", "gelsin"
    };

    private static readonly HashSet<string> EnglishWords = new(StringComparer.Ordinal)
    {
        "the", "and", "you", "that", "for", "with", "this", "have", "from", "hello",
        "world", "what", "your", "not", "are", "was", "please", "thanks", "thank",
        "would", "could", "should", "about", "which", "there", "their", "been", "were",
        "will", "just", "like", "more", "some", "time", "very", "when", "come", "here",
        "how", "make", "than", "them", "well", "into", "only", "other", "over", "such",
        "take", "also", "after", "first", "these", "where", "because", "something",
        "nothing", "click", "open", "close", "error", "file", "http", "www"
    };

    public static bool LooksTurkish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var ch in text)
        {
            if (ch is 'ç' or 'ğ' or 'ı' or 'ö' or 'ş' or 'ü' or 'Ç' or 'Ğ' or 'İ' or 'Ö' or 'Ş' or 'Ü')
                return true;
        }

        foreach (var word in Words(text))
        {
            if (TurkishWords.Contains(word))
                return true;
        }

        return false;
    }

    public static bool LooksEnglish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || LooksTurkish(text))
            return false;

        foreach (var word in Words(text))
        {
            if (EnglishWords.Contains(word))
                return true;
        }

        return false;
    }

    /// <summary>English when the text looks Turkish; otherwise unknown.</summary>
    public static string? HeuristicTarget(string? text) => LooksTurkish(text) ? "EN" : null;

    /// <summary>English when the text looks Turkish; Turkish otherwise.</summary>
    public static string InitialTarget(string? text) => HeuristicTarget(text) ?? "TR";

    /// <summary>When Turkish was requested and Turkish was detected, switch to English.</summary>
    public static string? RetryTarget(string attemptedTarget, string? detectedLanguage)
    {
        var attempted = NormalizeLang(attemptedTarget);
        var detected = NormalizeLang(detectedLanguage);
        if (attempted == "TR" && detected == "TR")
            return "EN";
        return null;
    }

    public static string NormalizeLang(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "";

        var token = code.Trim().Replace('_', '-');
        var dash = token.IndexOf('-');
        if (dash > 0)
            token = token[..dash];
        if (token.Length > 3)
            token = token[..3];
        return token.ToUpperInvariant();
    }

    public static bool AlreadyInTarget(string? line, string? target)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        var code = NormalizeLang(target);
        var script = DominantScript(line ?? "");
        if (script.Length > 0)
            return script == code;
        if (code == "TR")
            return LooksTurkish(line);
        if (code == "EN")
            return LooksEnglish(line);
        return false;
    }

    public static string GoogleCode(string? target)
    {
        var code = NormalizeLang(target);
        return code switch
        {
            "ZH" => "zh-CN",
            "" => "en",
            _ => code.ToLowerInvariant()
        };
    }

    public static string DeepLCode(string? target)
    {
        var code = NormalizeLang(target);
        return code switch
        {
            "ZH" => "ZH-HANS",
            "PT" => "PT-PT",
            "" => "EN",
            _ => code
        };
    }

    public static string RememberedTarget(string? code)
    {
        var normalized = NormalizeLang(code);
        return Targets.Contains(normalized) ? normalized : "TR";
    }

    private static string DominantScript(string text)
    {
        var cyrillic = 0;
        var arabic = 0;
        var kana = 0;
        var hangul = 0;
        var han = 0;
        foreach (var ch in text)
        {
            if (ch is >= '\u0400' and <= '\u04FF')
                cyrillic++;
            else if (ch is >= '\u0600' and <= '\u06FF')
                arabic++;
            else if (ch is >= '\u3040' and <= '\u30FF')
                kana++;
            else if (ch is >= '\uAC00' and <= '\uD7AF')
                hangul++;
            else if (ch is >= '\u4E00' and <= '\u9FFF')
                han++;
        }

        if (kana > 0)
            return "JA";
        var best = Math.Max(cyrillic, Math.Max(arabic, Math.Max(hangul, han)));
        if (best < 1)
            return "";
        if (cyrillic == best)
            return "RU";
        if (arabic == best)
            return "AR";
        if (hangul == best)
            return "KO";
        return "ZH";
    }

    private static IEnumerable<string> Words(string text)
    {
        var lower = text.ToLower(Turkish);
        var start = -1;
        for (var i = 0; i <= lower.Length; i++)
        {
            var letter = i < lower.Length && char.IsLetter(lower[i]);
            if (letter && start < 0)
                start = i;
            if (!letter && start >= 0)
            {
                if (i - start >= 2)
                    yield return lower.Substring(start, i - start);
                start = -1;
            }
        }
    }
}

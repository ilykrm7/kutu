using System.Text;
using System.Text.Json;

namespace Kutu.Core.Text;

/// <summary>
/// Splits long text only when a single request would be too large, and joins
/// provider segments in their original order. Whitespace between pieces is kept
/// as-is so newlines survive reassembly.
/// </summary>
public static class TranslationPlan
{
    public const int MyMemoryMaxChars = 500;

    public const int GoogleChunkChars = 4500;

    public const int DeepLChunkChars = 20000;

    public readonly record struct Piece(string Text, string Trailing);

    public static bool AllowMyMemory(string? text) =>
        !string.IsNullOrEmpty(text) && text.Length <= MyMemoryMaxChars;

    public static IReadOnlyList<Piece> Pieces(string? text, int maxChars)
    {
        if (maxChars < 1)
            maxChars = 1;
        text ??= "";
        if (text.Length <= maxChars)
            return new[] { new Piece(text, "") };

        var pieces = new List<Piece>();
        var start = 0;
        while (start < text.Length)
        {
            var breakAt = FindParagraphBreak(text, start);
            var end = breakAt < 0 ? text.Length : breakAt;
            var body = text.Substring(start, end - start);
            var trailing = "";
            var next = end;
            if (breakAt >= 0)
            {
                var length = ParagraphBreakLength(text, breakAt);
                trailing = text.Substring(breakAt, length);
                next = breakAt + length;
            }

            if (body.Length <= maxChars)
                pieces.Add(new Piece(body, trailing));
            else
                SplitSentences(body, trailing, maxChars, pieces);
            start = next;
        }

        return pieces.Count == 0 ? new[] { new Piece("", text) } : pieces;
    }

    public static async Task<string> TranslateInOrderAsync(
        string text,
        int maxChars,
        Func<string, CancellationToken, Task<string>> translate,
        CancellationToken cancellationToken)
    {
        var pieces = Pieces(text, maxChars);
        var builder = new StringBuilder();
        foreach (var piece in pieces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (piece.Text.Length == 0 || string.IsNullOrWhiteSpace(piece.Text))
                builder.Append(piece.Text);
            else
                builder.Append(await translate(piece.Text, cancellationToken).ConfigureAwait(false));
            builder.Append(piece.Trailing);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Google's unofficial payload is [ [ [translated, source, ...], ... ], ..., detected ].
    /// Every data[0][i][0] string is appended in array order. Source text and null slots are ignored.
    /// </summary>
    public static bool TryJoinGoogle(string? json, out string text, out string? detected)
    {
        text = "";
        detected = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return false;
            if (root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String)
                detected = root[2].GetString();
            if (root[0].ValueKind != JsonValueKind.Array)
                return false;

            var builder = new StringBuilder();
            var any = false;
            foreach (var part in root[0].EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Array || part.GetArrayLength() == 0)
                    continue;
                if (part[0].ValueKind != JsonValueKind.String)
                    continue;
                builder.Append(part[0].GetString());
                any = true;
            }

            text = builder.ToString();
            return any;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void SplitSentences(string body, string trailing, int maxChars, List<Piece> pieces)
    {
        var sentences = new List<string>();
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            if (!IsSentenceEnd(body, i))
                continue;
            var end = i + 1;
            while (end < body.Length && (body[end] == ' ' || body[end] == '\t'))
                end++;
            sentences.Add(body.Substring(start, end - start));
            start = end;
            i = end - 1;
        }

        if (start < body.Length)
            sentences.Add(body.Substring(start));
        if (sentences.Count <= 1)
        {
            SplitWords(body, trailing, maxChars, pieces);
            return;
        }

        var packed = Pack(sentences, maxChars);
        for (var i = 0; i < packed.Count; i++)
        {
            var tail = i == packed.Count - 1 ? trailing : "";
            if (packed[i].Length <= maxChars)
                pieces.Add(new Piece(packed[i], tail));
            else
                SplitWords(packed[i], tail, maxChars, pieces);
        }
    }

    private static void SplitWords(string body, string trailing, int maxChars, List<Piece> pieces)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] != ' ' && body[i] != '\t')
                continue;
            var end = i + 1;
            while (end < body.Length && (body[end] == ' ' || body[end] == '\t'))
                end++;
            words.Add(body.Substring(start, end - start));
            start = end;
            i = end - 1;
        }

        if (start < body.Length)
            words.Add(body.Substring(start));
        if (words.Count <= 1)
        {
            HardCut(body, trailing, maxChars, pieces);
            return;
        }

        var packed = Pack(words, maxChars);
        for (var i = 0; i < packed.Count; i++)
        {
            var tail = i == packed.Count - 1 ? trailing : "";
            if (packed[i].Length <= maxChars)
                pieces.Add(new Piece(packed[i], tail));
            else
                HardCut(packed[i], tail, maxChars, pieces);
        }
    }

    private static void HardCut(string body, string trailing, int maxChars, List<Piece> pieces)
    {
        for (var i = 0; i < body.Length; i += maxChars)
        {
            var length = Math.Min(maxChars, body.Length - i);
            var last = i + length >= body.Length;
            pieces.Add(new Piece(body.Substring(i, length), last ? trailing : ""));
        }
    }

    private static List<string> Pack(List<string> units, int maxChars)
    {
        var chunks = new List<string>();
        var builder = new StringBuilder();
        foreach (var unit in units)
        {
            if (unit.Length > maxChars)
            {
                if (builder.Length > 0)
                {
                    chunks.Add(builder.ToString());
                    builder.Clear();
                }

                chunks.Add(unit);
                continue;
            }

            if (builder.Length > 0 && builder.Length + unit.Length > maxChars)
            {
                chunks.Add(builder.ToString());
                builder.Clear();
            }

            builder.Append(unit);
        }

        if (builder.Length > 0)
            chunks.Add(builder.ToString());
        return chunks;
    }

    private static int FindParagraphBreak(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (ParagraphBreakLength(text, i) > 0)
                return i;
        }

        return -1;
    }

    private static int ParagraphBreakLength(string text, int index)
    {
        if (index + 3 < text.Length
            && text[index] == '\r' && text[index + 1] == '\n'
            && text[index + 2] == '\r' && text[index + 3] == '\n')
            return 4;
        if (index + 1 < text.Length && text[index] == '\n' && text[index + 1] == '\n')
            return 2;
        if (index + 1 < text.Length && text[index] == '\r' && text[index + 1] == '\r')
            return 2;
        return 0;
    }

    private static bool IsSentenceEnd(string text, int index)
    {
        var ch = text[index];
        if (ch is not ('.' or '!' or '?' or '…' or '。'))
            return false;
        if (ch == '.' && index + 1 < text.Length && (text[index + 1] == '.' || char.IsDigit(text[index + 1])))
            return false;
        return true;
    }
}

using Kutu.Core.Text;

namespace Kutu.Core.Tests;

public class TranslationPlanTests
{
    private const string Turkish = """
        Merhaba. Bu birinci paragrafın ilk cümlesi. İkinci cümle burada biter.

        İkinci paragraf daha kısadır.

        Üçüncü paragraf üç cümle içerir. Sıra bozulmamalı. Son cümle bu.

        Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır. Sıra korunur ve satır sonu kalır.
        """;

    private const string English = """
        Hello. This is the first sentence of the first paragraph. The second sentence ends here.

        The second paragraph is shorter.

        The third paragraph has three sentences. Order must hold. This is the last sentence.

        Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays. Order is kept and the line break stays.
        """;

    [Fact]
    public void Paragraphs_reassemble_in_source_order_and_keep_newlines()
    {
        AssertPieces(Turkish);
        AssertPieces(English);
        Assert.StartsWith("Merhaba", TranslationPlan.Pieces(Turkish, 40)[0].Text);
        Assert.StartsWith("Hello", TranslationPlan.Pieces(English, 40)[0].Text);
        Assert.Contains("Üçüncü", string.Concat(TranslationPlan.Pieces(Turkish, 40).Select(piece => piece.Text)));
        Assert.Contains("third paragraph", string.Concat(TranslationPlan.Pieces(English, 48).Select(piece => piece.Text)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chunks_are_translated_in_call_order_not_completion_order()
    {
        foreach (var sample in new[] { Turkish, English })
        {
            var seen = new List<string>();
            var result = await TranslationPlan.TranslateInOrderAsync(sample, 36, async (chunk, _) =>
            {
                var index = seen.Count;
                seen.Add(chunk);
                await Task.Delay(index % 2 == 0 ? 8 : 1);
                return index + ":" + chunk;
            }, CancellationToken.None);

            Assert.True(seen.Count > 1);
            var cursor = 0;
            for (var i = 0; i < seen.Count; i++)
            {
                var token = i + ":" + seen[i];
                var at = result.IndexOf(token, cursor, StringComparison.Ordinal);
                Assert.True(at >= cursor, sample[..Math.Min(24, sample.Length)]);
                cursor = at + token.Length;
            }

            var gap = sample.IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0 ? "\r\n\r\n" : "\n\n";
            Assert.Contains(gap, result);
            Assert.All(seen, chunk => Assert.DoesNotContain(gap, chunk));
        }
    }

    [Fact]
    public void Google_segments_are_joined_in_array_order()
    {
        const string json = """
            [
              [
                ["Üç. ", "One. ", null, null, 1],
                null,
                ["İki. ", "Two. ", null, null, 1],
                ["Bir.", "Three.", null, null, 1]
              ],
              null,
              "en"
            ]
            """;

        Assert.True(TranslationPlan.TryJoinGoogle(json, out var text, out var detected));
        Assert.Equal("Üç. İki. Bir.", text);
        Assert.Equal("en", detected);
        Assert.DoesNotContain("One", text);
        Assert.DoesNotContain("Two", text);
        Assert.DoesNotContain("Three", text);
    }

    [Fact]
    public void Google_multiline_segments_keep_paragraph_breaks()
    {
        const string json = """
            [[["Merhaba.\n\n","Hello.\n\n",null,null,1],["İkinci paragraf.","Second paragraph.",null,null,1]],null,"en"]
            """;

        Assert.True(TranslationPlan.TryJoinGoogle(json, out var text, out _));
        Assert.Equal("Merhaba.\n\nİkinci paragraf.", text);
    }

    [Fact]
    public void MyMemory_is_only_for_short_text()
    {
        Assert.True(TranslationPlan.AllowMyMemory("merhaba"));
        Assert.True(TranslationPlan.AllowMyMemory(new string('a', 500)));
        Assert.False(TranslationPlan.AllowMyMemory(new string('a', 501)));
        Assert.True(Turkish.Length > TranslationPlan.MyMemoryMaxChars);
        Assert.True(English.Length > TranslationPlan.MyMemoryMaxChars);
        Assert.False(TranslationPlan.AllowMyMemory(Turkish));
        Assert.False(TranslationPlan.AllowMyMemory(English));
    }

    private static void AssertPieces(string text)
    {
        foreach (var max in new[] { 24, 40, 80, TranslationPlan.GoogleChunkChars })
        {
            var pieces = TranslationPlan.Pieces(text, max);
            var rebuilt = string.Concat(pieces.Select(piece => piece.Text + piece.Trailing));
            Assert.Equal(text, rebuilt);
            Assert.All(pieces, piece => Assert.True(piece.Text.Length <= max));
        }
    }
}

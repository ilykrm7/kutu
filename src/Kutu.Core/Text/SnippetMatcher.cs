using Kutu.Core.Models;

namespace Kutu.Core.Text;

public static class SnippetMatcher
{
    public static Snippet? Match(string buffer, IEnumerable<Snippet> snippets)
    {
        if (string.IsNullOrEmpty(buffer))
            return null;

        Snippet? best = null;
        foreach (var snippet in snippets)
        {
            var trigger = snippet.Trigger?.Trim() ?? "";
            if (trigger.Length == 0 || trigger.Length > buffer.Length)
                continue;

            if (!buffer.EndsWith(trigger, StringComparison.Ordinal))
                continue;

            var start = buffer.Length - trigger.Length;
            if (start > 0 && !char.IsWhiteSpace(buffer[start - 1]))
                continue;

            if (best is null || trigger.Length > (best.Trigger?.Trim().Length ?? 0))
                best = snippet;
        }

        return best;
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace Kutu.Core.Text;

public static class ScreenLinePlan
{
    private static readonly Regex Marker = new(@"\[\[(\d+)\]\]\s*", RegexOptions.Compiled);

    public sealed record Line(string Text, bool Skip);

    public static Line[] Mark(IReadOnlyList<string> lines, string target)
    {
        var marked = new Line[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            var text = (lines[i] ?? "").Replace('\r', ' ').Trim();
            marked[i] = new Line(text, TranslateDirection.AlreadyInTarget(text, target));
        }

        return marked;
    }

    public static bool ShowTranslation(string? source, string? translation)
    {
        if (string.IsNullOrWhiteSpace(translation))
            return false;
        return !string.Equals((source ?? "").Trim(), translation.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> Pending(IReadOnlyList<Line> lines)
    {
        var pending = new List<string>();
        foreach (var line in lines)
        {
            if (!line.Skip && line.Text.Length > 0)
                pending.Add(line.Text);
        }

        return pending;
    }

    public static IReadOnlyList<string> Pack(IReadOnlyList<string> lines, int batchSize = 24)
    {
        if (batchSize < 1)
            batchSize = 24;
        var batches = new List<string>();
        for (var offset = 0; offset < lines.Count; offset += batchSize)
        {
            var count = Math.Min(batchSize, lines.Count - offset);
            var builder = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append('\n');
                builder.Append("[[").Append(i + 1).Append("]] ").Append(lines[offset + i].Replace('\n', ' '));
            }

            batches.Add(builder.ToString());
        }

        return batches;
    }

    public static string[] Unpack(string translated, int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
            result[i] = "";
        if (string.IsNullOrWhiteSpace(translated) || count == 0)
            return result;

        var matches = Marker.Matches(translated);
        if (matches.Count == 0)
        {
            if (count == 1)
                result[0] = translated.Trim();
            return result;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            if (!int.TryParse(matches[i].Groups[1].Value, out var number))
                continue;
            var index = number - 1;
            if (index < 0 || index >= count)
                continue;
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : translated.Length;
            result[index] = translated.Substring(start, Math.Max(0, end - start)).Trim();
        }

        return result;
    }
}

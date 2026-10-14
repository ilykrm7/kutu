namespace Kutu.Imaging;

public static class ImageFileList
{
    public static void AddDistinct(List<string> files, IEnumerable<string> incoming)
    {
        foreach (var full in Distinct(incoming))
        {
            if (files.Exists(existing => existing.Equals(full, StringComparison.OrdinalIgnoreCase)))
                continue;
            files.Add(full);
        }
    }

    public static List<string> Distinct(IEnumerable<string> paths)
    {
        var files = new List<string>();
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw) || !ImageFormats.IsSupported(raw))
                continue;

            string full;
            try
            {
                full = Path.GetFullPath(raw);
            }
            catch
            {
                continue;
            }

            if (files.Exists(existing => existing.Equals(full, StringComparison.OrdinalIgnoreCase)))
                continue;
            files.Add(full);
        }

        return files;
    }

    public static List<string> CombineSelection(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return [];
        if (segments.Count == 1)
            return Distinct([segments[0]]);

        var directory = segments[0];
        var paths = new List<string>(segments.Count - 1);
        for (var i = 1; i < segments.Count; i++)
        {
            var name = segments[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;
            paths.Add(Path.IsPathRooted(name) ? name : Path.Combine(directory, name));
        }

        return Distinct(paths);
    }
}

namespace Kutu.Core.Commands;

public static class ConvertArgs
{
    public static bool IsHeadless(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (arg.Equals("--convert-to", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool TryParse(IReadOnlyList<string> args, out string format, out IReadOnlyList<string> files)
    {
        format = "";
        files = [];
        var index = -1;
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals("--convert-to", StringComparison.OrdinalIgnoreCase))
                continue;
            index = i;
            break;
        }

        if (index < 0 || index + 1 >= args.Count)
            return false;

        format = args[index + 1];
        var list = new List<string>();
        for (var i = index + 2; i < args.Count; i++)
        {
            if (args[i].StartsWith("-", StringComparison.Ordinal))
                break;
            var path = args[i].Trim().Trim('"');
            if (path.Length > 0)
                list.Add(path);
        }

        files = list;
        return true;
    }
}

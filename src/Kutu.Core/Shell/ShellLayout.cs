namespace Kutu.Core.Shell;

/// <summary>
/// Files older builds left beside Kutu.exe. The classic menu does not need them.
/// </summary>
public static class ShellLayout
{
    public static readonly string[] LegacyNeighborNames =
    [
        "KutuShell.dll",
        "KutuShell.exe",
        "Square44x44Logo.png",
        "Square150x150Logo.png",
        "StoreLogo.png",
        "kutu.path",
        "kutu.sparse",
        "kutu.sparse.txt",
        "AppxManifest.xml"
    ];

    public static bool IsGeneratedScript(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var name = Path.GetFileName(path.Replace('\\', '/'));
        if (string.IsNullOrEmpty(name) || name.Length < 12 || !name.StartsWith("kutu-shell", StringComparison.OrdinalIgnoreCase))
            return false;

        var dot = name.LastIndexOf('.');
        if (dot < 0 || name.Length - dot != 4)
            return false;

        var a = char.ToLowerInvariant(name[dot + 1]);
        var b = char.ToLowerInvariant(name[dot + 2]);
        var c = char.ToLowerInvariant(name[dot + 3]);
        return (a == 'p' && b == 's' && c == '1')
            || (a == 'b' && b == 'a' && c == 't')
            || (a == 'c' && b == 'm' && c == 'd');
    }

    public static string VersionFolder(string appDirectory, string version)
    {
        if (string.IsNullOrWhiteSpace(appDirectory))
            throw new ArgumentException("App directory is required.", nameof(appDirectory));
        if (string.IsNullOrWhiteSpace(version) || version.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            throw new ArgumentException("Version must be a single folder name.", nameof(version));

        return Path.Combine(appDirectory, "shell", version);
    }

    public static string StampText(string version, string payloadId, string exe, string result) =>
        version + "\n" + payloadId + "\n" + exe + "\n" + result + "\n";

    public static bool Matches(string? stamp, string version, string payloadId, string exe)
    {
        var lines = Lines(stamp);
        return lines is not null
            && lines[0] == version
            && lines[1] == payloadId
            && lines[2].Equals(exe, StringComparison.OrdinalIgnoreCase)
            && (lines[3] == "ok" || lines[3] == "classic-only");
    }

    public static bool Succeeded(string? stamp)
    {
        var lines = Lines(stamp);
        return lines is not null && lines[3] == "ok";
    }

    public static bool SharesExecutableDirectory(string shellDir, string exe)
    {
        var exeDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(exeDir) || string.IsNullOrWhiteSpace(shellDir))
            return false;

        return string.Equals(Normalize(shellDir), Normalize(exeDir), StringComparison.OrdinalIgnoreCase);
    }

    private static string[]? Lines(string? stamp)
    {
        if (stamp is null || stamp.Trim().Length == 0)
            return null;

        var lines = stamp.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return lines.Length >= 4 ? lines : null;
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

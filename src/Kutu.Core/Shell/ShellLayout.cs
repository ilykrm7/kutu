namespace Kutu.Core.Shell;

/// <summary>
/// Where the Windows 11 sparse package lives. It is never the folder that contains Kutu.exe.
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
        "kutu-shell.ps1",
        "kutu.sparse",
        "kutu.sparse.txt",
        "AppxManifest.xml"
    ];

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

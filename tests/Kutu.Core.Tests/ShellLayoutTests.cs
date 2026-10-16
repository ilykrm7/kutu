using Kutu.Core.Shell;

namespace Kutu.Core.Tests;

public class ShellLayoutTests
{
    [Fact]
    public void Package_lives_in_a_versioned_appdata_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "kutu-shell-layout");
        var app = Path.Combine(root, "Roaming", "Kutu");
        var download = Path.Combine(root, "Downloads", "Kutu.exe");
        var shell = ShellLayout.VersionFolder(app, "1.1.0");

        Assert.Equal(Path.Combine(app, "shell", "1.1.0"), shell);
        Assert.False(ShellLayout.SharesExecutableDirectory(shell, download));
    }

    [Fact]
    public void Stamp_retries_only_when_the_version_payload_or_exe_changes()
    {
        var exe = Path.Combine("dist", "Kutu.exe");
        var stamp = ShellLayout.StampText("1.1.0", "abc", exe, "ok");
        Assert.True(ShellLayout.Matches(stamp, "1.1.0", "abc", exe));
        Assert.True(ShellLayout.Succeeded(stamp));
        Assert.True(ShellLayout.Matches(stamp.Replace("\n", "\r\n"), "1.1.0", "abc", exe.ToUpperInvariant()));

        Assert.False(ShellLayout.Matches(stamp, "1.2.0", "abc", exe));
        Assert.False(ShellLayout.Matches(stamp, "1.1.0", "def", exe));
        Assert.False(ShellLayout.Matches(stamp, "1.1.0", "abc", Path.Combine("other", "Kutu.exe")));
        Assert.False(ShellLayout.Succeeded(ShellLayout.StampText("1.1.0", "abc", exe, "classic-only")));
        Assert.True(ShellLayout.Matches(ShellLayout.StampText("1.1.0", "abc", exe, "classic-only"), "1.1.0", "abc", exe));
    }

    [Fact]
    public void Legacy_neighbors_are_the_shell_files_that_used_to_sit_beside_the_exe()
    {
        Assert.Contains("KutuShell.dll", ShellLayout.LegacyNeighborNames);
        Assert.Contains("Square44x44Logo.png", ShellLayout.LegacyNeighborNames);
        Assert.Contains("Square150x150Logo.png", ShellLayout.LegacyNeighborNames);
        Assert.Contains("kutu.sparse.txt", ShellLayout.LegacyNeighborNames);
        Assert.Contains("kutu.path", ShellLayout.LegacyNeighborNames);
        Assert.DoesNotContain("Kutu.exe", ShellLayout.LegacyNeighborNames);
        foreach (var name in ShellLayout.LegacyNeighborNames)
            Assert.False(ShellLayout.IsGeneratedScript(name));
    }

    [Fact]
    public void Generated_script_names_are_recognized_without_being_stored()
    {
        Assert.True(ShellLayout.IsGeneratedScript("kutu-shell.ps1"));
        Assert.True(ShellLayout.IsGeneratedScript("kutu-shell-remove.ps1"));
        Assert.True(ShellLayout.IsGeneratedScript(@"C:\Temp\kutu-shell.bat"));
        Assert.True(ShellLayout.IsGeneratedScript("kutu-shell.cmd"));
        Assert.False(ShellLayout.IsGeneratedScript("kutu-shell.log"));
        Assert.False(ShellLayout.IsGeneratedScript("notes.ps1"));
        Assert.False(ShellLayout.IsGeneratedScript("Kutu.exe"));
    }
}

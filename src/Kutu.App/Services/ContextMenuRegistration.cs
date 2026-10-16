using System.Runtime.InteropServices;
using Kutu.Core;
using Kutu.Core.Settings;
using Kutu.Core.Shell;
using Microsoft.Win32;

namespace Kutu.App.Services;

public static class ContextMenuRegistration
{
    private static readonly string[] Extensions =
    [
        ".png", ".jpg", ".jpeg", ".jfif", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".heic", ".heif"
    ];

    private static readonly (string Id, string Format, string Title)[] Formats =
    [
        ("01jpeg", "jpeg", "JPEG"),
        ("02png", "png", "PNG"),
        ("03webp", "webp", "WebP"),
        ("04ico", "ico", "ICO"),
        ("05pdf", "pdf", "PDF"),
        ("06bmp", "bmp", "BMP")
    ];

    private static readonly string[] LegacyStoreIds =
    [
        "Kutu.ToJpeg", "Kutu.ToPng", "Kutu.ToWebp", "Kutu.ToIco", "Kutu.ToPdf", "Kutu.ToBmp"
    ];

    public static string SetEnabled(bool enabled)
    {
        var exe = HostProcess.Executable ?? "";
        SetClassic(enabled, exe);
        if (!string.IsNullOrWhiteSpace(exe))
            RemoveLegacyNeighbors(exe);
        RemoveGeneratedScripts();
        Notify();
        return "";
    }

    public static void EnsureClassic(string? exe)
    {
        RemoveGeneratedScripts();
        if (!string.IsNullOrWhiteSpace(exe))
            RemoveLegacyNeighbors(exe);
        if (string.IsNullOrWhiteSpace(exe) || ClassicCurrent(exe))
            return;

        SetClassic(true, exe);
        Notify();
    }

    public static void RemoveClassic()
    {
        SetClassic(false, "");
        Notify();
    }

    public static bool HasClassicMenu()
    {
        try
        {
            using var shell = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\.png\shell\Kutu");
            return shell is not null;
        }
        catch
        {
            return false;
        }
    }

    public static string CommandLine(string exe, string format) =>
        $"\"{exe}\" --convert-to {format} \"%1\"";

    private static bool ClassicCurrent(string exe)
    {
        using var shell = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\.png\shell\Kutu");
        using var command = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\.png\shell\Kutu\shell\01jpeg\command");
        return shell?.GetValue("SubCommands") as string == ""
            && command?.GetValue("") as string == CommandLine(exe, "jpeg");
    }

    private static void RemoveLegacyNeighbors(string exe)
    {
        var dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(dir))
            return;

        foreach (var name in ShellLayout.LegacyNeighborNames)
        {
            try
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // A read-only folder still keeps the classic menu.
            }
        }

        DeleteGeneratedScripts(dir, searchSubdirectories: false);
    }

    private static void RemoveGeneratedScripts()
    {
        try
        {
            DeleteGeneratedScripts(Path.GetTempPath(), searchSubdirectories: false);
        }
        catch
        {
            // Temp cleanup must not block the menu.
        }

        var root = Path.Combine(SettingsService.AppDirectory, "shell");
        if (!Directory.Exists(root))
            return;

        DeleteGeneratedScripts(root, searchSubdirectories: true);
    }

    private static void DeleteGeneratedScripts(string directory, bool searchSubdirectories)
    {
        try
        {
            var option = searchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var path in Directory.EnumerateFiles(directory, "kutu-shell*", option))
            {
                if (!ShellLayout.IsGeneratedScript(path))
                    continue;
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // The file is in use. The next start tries again.
                }
            }
        }
        catch
        {
            // Missing directories are already clean.
        }
    }

    private static void SetClassic(bool enabled, string exe)
    {
        foreach (var id in LegacyStoreIds)
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell\{id}", throwOnMissingSubKey: false);

        foreach (var extension in Extensions)
        {
            var legacy = $@"Software\Classes\SystemFileAssociations\{extension}\shell\Kutu.Convert";
            var path = $@"Software\Classes\SystemFileAssociations\{extension}\shell\Kutu";
            Registry.CurrentUser.DeleteSubKeyTree(legacy, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            if (!enabled || string.IsNullOrWhiteSpace(exe))
                continue;

            using var key = Registry.CurrentUser.CreateSubKey(path);
            if (key is null)
                continue;

            key.SetValue("MUIVerb", "Kutu ile dönüştür");
            key.SetValue("Icon", $"\"{exe}\",0");
            // Empty SubCommands tells Explorer to enumerate shell\Kutu\shell.
            // A non-empty list is resolved only from HKLM CommandStore, which a
            // per-user install cannot write, so the submenu would be empty.
            key.SetValue("SubCommands", "", RegistryValueKind.String);
            using var verbs = key.CreateSubKey("shell");
            foreach (var format in Formats)
            {
                using var verb = verbs?.CreateSubKey(format.Id);
                verb?.SetValue("MUIVerb", format.Title);
                using var command = verb?.CreateSubKey("command");
                command?.SetValue("", CommandLine(exe, format.Format));
            }
        }
    }

    private static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}

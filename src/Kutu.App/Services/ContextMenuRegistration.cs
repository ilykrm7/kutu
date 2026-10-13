using Microsoft.Win32;

namespace Kutu.App.Services;

public static class ContextMenuRegistration
{
    private static readonly string[] Extensions =
    [
        ".png", ".jpg", ".jpeg", ".jfif", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".heic", ".heif"
    ];

    public static void SetEnabled(bool enabled)
    {
        foreach (var extension in Extensions)
        {
            var path = $@"Software\Classes\SystemFileAssociations\{extension}\shell\Kutu.Convert";
            if (!enabled)
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                continue;
            }

            using var key = Registry.CurrentUser.CreateSubKey(path);
            key?.SetValue("", "Kutu ile dönüştür");
            var exe = Environment.ProcessPath ?? "";
            key?.SetValue("Icon", $"\"{exe}\",0");
            using var command = key?.CreateSubKey("command");
            command?.SetValue("", $"\"{exe}\" --convert \"%1\"");
        }
    }
}

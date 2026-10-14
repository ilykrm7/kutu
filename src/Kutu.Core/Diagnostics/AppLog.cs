using Kutu.Core.Settings;

namespace Kutu.Core.Diagnostics;

public static class AppLog
{
    public static void Write(string message)
    {
        try
        {
            var dir = SettingsService.AppDirectory;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "kutu.log"), $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not crash the app.
        }
    }

    public static void Write(Exception exception) => Write(exception.ToString());
}

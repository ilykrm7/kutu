using Kutu.Core;
using Microsoft.Win32;

namespace Kutu.App.Services;

public static class StartupRegistration
{
    private const string ValueName = "Kutu";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{HostProcess.Executable}\" --tray");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

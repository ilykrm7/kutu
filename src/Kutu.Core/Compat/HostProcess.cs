namespace Kutu.Core;

public static class HostProcess
{
    public static string? Executable
    {
        get
        {
#if NET
            return Environment.ProcessPath;
#else
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
#endif
        }
    }

    public static bool IsWindows
    {
        get
        {
#if NET
            return OperatingSystem.IsWindows();
#else
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
        }
    }
}

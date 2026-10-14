using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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
            var module = ModuleFileName();
            if (!string.IsNullOrWhiteSpace(module))
                return module;

            try
            {
                var main = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(main))
                    return main;
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }

            var location = Assembly.GetEntryAssembly()?.Location;
            return string.IsNullOrWhiteSpace(location) ? null : location;
#endif
        }
    }

#if !NET
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetModuleFileName(IntPtr module, StringBuilder filename, int size);

    private static string? ModuleFileName()
    {
        var buffer = new StringBuilder(32768);
        var length = GetModuleFileName(IntPtr.Zero, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : null;
    }
#endif

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

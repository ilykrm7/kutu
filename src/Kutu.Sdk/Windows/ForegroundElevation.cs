using System.Runtime.InteropServices;
using System.Security.Principal;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;

namespace Kutu.Sdk.Windows;

public static class ForegroundElevation
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;
    private static readonly bool CurrentProcessElevated = QueryCurrentProcess();
    private static int _noticed;

    public static bool BlocksTyping()
    {
        if (!OperatingSystem.IsWindows() || CurrentProcessElevated)
            return false;

        try
        {
            return ForegroundLooksElevated();
        }
        catch
        {
            return false;
        }
    }

    public static void NotifyOnce(IKutuHost host)
    {
        if (Interlocked.Exchange(ref _noticed, 1) != 0)
            return;

        host.NotifyTray(L.Get("Hook_Elevated"));
    }

    private static bool QueryCurrentProcess()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static bool ForegroundLooksElevated()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId)
            return false;

        var process = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (process == IntPtr.Zero)
            return true;

        try
        {
            if (!OpenProcessToken(process, TokenQuery, out var token))
                return true;

            try
            {
                if (!GetTokenInformation(token, TokenElevation, out var elevation, sizeof(int), out _))
                    return true;
                return elevation != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int informationClass, out int elevation, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

using System.Diagnostics;

namespace Kutu.Core.IO;

public static class WinProcess
{
    public static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        if (value.IndexOfAny([' ', '\t', '"']) < 0)
            return value;
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    public static string Join(IEnumerable<string> args) => string.Join(" ", args.Select(Quote));

    public static void KillTree(int processId)
    {
        try
        {
            using var killer = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/PID " + processId + " /T /F",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            killer?.WaitForExit(4000);
        }
        catch
        {
            try
            {
                Process.GetProcessById(processId).Kill();
            }
            catch
            {
                // The process has already exited.
            }
        }
    }
}

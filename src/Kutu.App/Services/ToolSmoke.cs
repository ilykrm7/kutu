using Kutu.Core.Diagnostics;
using Kutu.Core.Settings;
using Kutu.Sdk.Hosting;

namespace Kutu.App.Services;

internal static class ToolSmoke
{
    public const string Argument = "--smoke-tools";

    public static bool Requested(string[] args) =>
        args.Any(arg => arg.Equals(Argument, StringComparison.OrdinalIgnoreCase));

    public static int Run(IKutuHost host)
    {
        var built = new List<string>();
        var failed = new List<string>();
        foreach (var tool in host.Tools)
        {
            try
            {
                if (tool.CreateView(host) is null)
                    failed.Add(tool.Id + ":null");
                else
                    built.Add(tool.Id);
            }
            catch (Exception ex)
            {
                failed.Add(tool.Id);
                AppLog.Write("smoke " + tool.Id + " " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        var path = Path.Combine(SettingsService.AppDirectory, "smoke-tools.txt");
        Directory.CreateDirectory(SettingsService.AppDirectory);
        File.WriteAllLines(path, built);
        if (failed.Count == 0)
            return 0;

        AppLog.Write("smoke failed " + string.Join(" ", failed));
        return 1;
    }
}

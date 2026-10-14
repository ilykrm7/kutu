using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Kutu.App.Services;

public static class ContextMenuRegistration
{
    private const string PackageName = "KerimEksi.Kutu.Shell";
    private const string Clsid = "B6E1A2C4-7D33-4F1A-9C58-2E6A4B8D1F70";

    private static readonly string[] Extensions =
    [
        ".png", ".jpg", ".jpeg", ".jfif", ".webp", ".bmp", ".gif", ".tif", ".tiff", ".ico", ".heic", ".heif"
    ];

    private static readonly (string Id, string Format, string Title)[] Formats =
    [
        ("Kutu.ToJpeg", "jpeg", "JPEG"),
        ("Kutu.ToPng", "png", "PNG"),
        ("Kutu.ToWebp", "webp", "WebP"),
        ("Kutu.ToIco", "ico", "ICO"),
        ("Kutu.ToPdf", "pdf", "PDF"),
        ("Kutu.ToBmp", "bmp", "BMP")
    ];

    public static string SetEnabled(bool enabled)
    {
        var exe = Environment.ProcessPath ?? "";
        SetClassic(enabled, exe);
        if (!enabled)
        {
            TryRemoveSparse();
            Notify();
            return "";
        }

        var modern = TryInstallSparse(exe);
        StampSparse(exe, modern);
        Notify();
        return modern;
    }

    public static void EnsureClassic(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe) || ClassicCurrent(exe))
            return;

        SetClassic(true, exe);
        Notify();
    }

    public static void EnsureSparse(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe))
            return;

        var dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(dir))
            return;

        var stamp = Path.Combine(dir, "kutu.sparse.txt");
        try
        {
            if (File.Exists(stamp) && File.ReadAllText(stamp).StartsWith(exe + "\n", StringComparison.OrdinalIgnoreCase))
                return;
        }
        catch
        {
            return;
        }

        StampSparse(exe, TryInstallSparse(exe));
    }

    public static string CommandLine(string exe, string format) =>
        $"\"{exe}\" --convert-to {format} \"%1\"";

    private static bool ClassicCurrent(string exe)
    {
        using var command = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell\Kutu.ToJpeg\command");
        using var shell = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\.png\shell\Kutu");
        return shell is not null && command?.GetValue("") as string == CommandLine(exe, "jpeg");
    }

    private static void StampSparse(string exe, string result)
    {
        var dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(dir))
            return;

        try
        {
            File.WriteAllText(Path.Combine(dir, "kutu.sparse.txt"), exe + "\n" + result);
        }
        catch
        {
            // A read-only folder still keeps the classic menu.
        }
    }

    private static void SetClassic(bool enabled, string exe)
    {
        foreach (var format in Formats)
        {
            var store = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell\{format.Id}";
            if (!enabled)
            {
                Registry.CurrentUser.DeleteSubKeyTree(store, throwOnMissingSubKey: false);
                continue;
            }

            using var key = Registry.CurrentUser.CreateSubKey(store);
            key?.SetValue("MUIVerb", format.Title);
            using var command = key?.CreateSubKey("command");
            command?.SetValue("", CommandLine(exe, format.Format));
        }

        var subCommands = string.Join(';', Formats.Select(item => item.Id));
        foreach (var extension in Extensions)
        {
            var legacy = $@"Software\Classes\SystemFileAssociations\{extension}\shell\Kutu.Convert";
            var path = $@"Software\Classes\SystemFileAssociations\{extension}\shell\Kutu";
            Registry.CurrentUser.DeleteSubKeyTree(legacy, throwOnMissingSubKey: false);
            if (!enabled)
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                continue;
            }

            using var key = Registry.CurrentUser.CreateSubKey(path);
            key?.DeleteSubKeyTree("command", throwOnMissingSubKey: false);
            key?.SetValue("MUIVerb", "Kutu ile dönüştür");
            key?.SetValue("Icon", $"\"{exe}\",0");
            key?.SetValue("SubCommands", subCommands);
        }
    }

    private static string TryInstallSparse(string exe)
    {
        var dir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(dir))
            return "classic-only";

        try
        {
            var probe = Path.Combine(dir, ".kutu-write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch
        {
            return "classic-only";
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var dll = assembly.GetManifestResourceStream("KutuShell.dll");
        using var logo = assembly.GetManifestResourceStream("KutuMark.png");
        if (dll is null || logo is null)
            return "classic-only";

        var logoBytes = ReadAll(logo);
        WriteStream(dll, Path.Combine(dir, "KutuShell.dll"));
        File.WriteAllBytes(Path.Combine(dir, "StoreLogo.png"), logoBytes);
        File.WriteAllBytes(Path.Combine(dir, "Square44x44Logo.png"), logoBytes);
        File.WriteAllBytes(Path.Combine(dir, "Square150x150Logo.png"), logoBytes);
        File.WriteAllText(Path.Combine(dir, "kutu.path"), exe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var manifest = Path.Combine(dir, "AppxManifest.xml");
        File.WriteAllText(manifest, Manifest(Path.GetFileName(exe)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return RunInstaller(manifest, dir) ? "" : "classic-only";
    }

    private static bool RunInstaller(string manifest, string dir)
    {
        var script = Path.Combine(dir, "kutu-shell.ps1");
        File.WriteAllText(script, $$"""
            $ErrorActionPreference = 'Stop'
            Get-AppxPackage -Name '{{PackageName}}' | Remove-AppxPackage -ErrorAction SilentlyContinue
            try {
              Add-AppxPackage -Path '{{manifest.Replace("'", "''")}}' -ExternalLocation '{{dir.Replace("'", "''")}}' -AllowUnsigned
            } catch {
              Add-AppxPackage -Path '{{manifest.Replace("'", "''")}}' -ExternalLocation '{{dir.Replace("'", "''")}}'
            }
            """);
        return RunPowerShell(script);
    }

    private static void TryRemoveSparse()
    {
        var script = Path.Combine(Path.GetTempPath(), "kutu-shell-remove.ps1");
        File.WriteAllText(script, $"Get-AppxPackage -Name '{PackageName}' | Remove-AppxPackage -ErrorAction SilentlyContinue");
        RunPowerShell(script);
    }

    private static bool RunPowerShell(string script)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
                return false;
            if (!process.WaitForExit(20000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Manifest(string exeName)
    {
        var items = string.Join('\n', Extensions.Select(ext =>
            $"""    <desktop5:ItemType Type="{ext}"><desktop5:Verb Id="KutuConvert" Clsid="{Clsid}" /></desktop5:ItemType>"""));
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                     xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
                     xmlns:desktop4="http://schemas.microsoft.com/appx/manifest/desktop/windows10/4"
                     xmlns:desktop5="http://schemas.microsoft.com/appx/manifest/desktop/windows10/5"
                     xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10"
                     IgnorableNamespaces="uap uap10 desktop4 desktop5 com">
              <Identity Name="{PackageName}" Publisher="CN=Kerim Eksi, O=Kutu, C=TR" Version="1.0.0.0" />
              <Properties>
                <DisplayName>Kutu</DisplayName>
                <PublisherDisplayName>Kerim Ekşi</PublisherDisplayName>
                <Logo>StoreLogo.png</Logo>
              </Properties>
              <Resources>
                <Resource Language="tr-tr" />
              </Resources>
              <Dependencies>
                <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22000.0" MaxVersionTested="10.0.26100.0" />
              </Dependencies>
              <Applications>
                <Application Id="Kutu" Executable="{exeName}" uap10:TrustLevel="mediumIL" uap10:RuntimeBehavior="win32App">
                  <uap:VisualElements DisplayName="Kutu" Description="Kutu" BackgroundColor="transparent" Square150x150Logo="Square150x150Logo.png" Square44x44Logo="Square44x44Logo.png" />
                </Application>
              </Applications>
              <Extensions>
                <desktop4:Extension Category="windows.fileExplorerContextMenus">
                  <desktop4:FileExplorerContextMenus>
            {items}
                  </desktop4:FileExplorerContextMenus>
                </desktop4:Extension>
                <com:Extension Category="windows.comServer">
                  <com:ComServer>
                    <com:SurrogateServer DisplayName="Kutu Shell">
                      <com:Class Id="{Clsid}" Path="KutuShell.dll" ThreadingModel="STA" />
                    </com:SurrogateServer>
                  </com:ComServer>
                </com:Extension>
              </Extensions>
            </Package>
            """;
    }

    private static void WriteStream(Stream source, string path)
    {
        using var file = File.Create(path);
        source.CopyTo(file);
    }

    private static byte[] ReadAll(Stream source)
    {
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        return memory.ToArray();
    }

    private static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}

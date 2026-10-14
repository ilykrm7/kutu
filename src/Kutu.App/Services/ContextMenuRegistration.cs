using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Kutu.Core;
using Kutu.Core.Settings;
using Kutu.Core.Shell;
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
        RemoveLegacyNeighbors(exe);
        if (!enabled)
        {
            TryRemoveSparse();
            Notify();
            return "";
        }

        var modern = Install(exe);
        Notify();
        return modern == "ok" ? "" : "classic-only";
    }

    public static void EnsureClassic(string? exe)
    {
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

    public static void EnsureSparse(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe))
            return;

        try
        {
            RemoveLegacyNeighbors(exe);
            if (StampMatches(exe))
                return;

            Install(exe);
        }
        catch
        {
            // The classic menu is already registered.
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

    private static bool StampMatches(string exe)
    {
        var dir = ShellDirectory();
        var stampPath = Path.Combine(dir, "kutu.sparse");
        if (!File.Exists(stampPath) || !File.Exists(Path.Combine(dir, "KutuShell.dll")) || !File.Exists(Path.Combine(dir, "kutu.path")))
            return false;

        try
        {
            return ShellLayout.Matches(File.ReadAllText(stampPath), VersionLabel(), PayloadId(), exe);
        }
        catch
        {
            return false;
        }
    }

    private static string Install(string exe)
    {
        string dir;
        try
        {
            dir = ShellDirectory();
            if (ShellLayout.SharesExecutableDirectory(dir, exe))
                return "classic-only";

            Directory.CreateDirectory(dir);
        }
        catch
        {
            return "classic-only";
        }

        // dllhost may still have the previous DLL mapped. Drop the package before replacing it.
        TryRemoveSparse();
        var result = "classic-only";
        try
        {
            if (Extract(dir))
            {
                File.WriteAllText(Path.Combine(dir, "kutu.path"), exe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                var manifest = Path.Combine(dir, "AppxManifest.xml");
                File.WriteAllText(manifest, Manifest(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (RunInstaller(manifest, dir))
                    result = "ok";
            }
        }
        catch
        {
            result = "classic-only";
        }

        try
        {
            File.WriteAllText(Path.Combine(dir, "kutu.sparse"), ShellLayout.StampText(VersionLabel(), PayloadId(), exe, result), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            RemoveOldVersions(dir);
        }
        catch
        {
            // The classic menu does not need the stamp.
        }

        return result;
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
    }

    private static void RemoveOldVersions(string current)
    {
        var root = Path.GetDirectoryName(current);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;

        foreach (var child in Directory.GetDirectories(root))
        {
            if (string.Equals(child, current, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                Directory.Delete(child, recursive: true);
            }
            catch
            {
                // dllhost can keep the previous DLL open until Explorer restarts.
            }
        }
    }

    private static bool Extract(string dir)
    {
        var assembly = Assembly.GetExecutingAssembly();
        if (!WriteResource(assembly, "KutuShell.dll", Path.Combine(dir, "KutuShell.dll")))
            return false;
        if (!WriteResource(assembly, "KutuShell.exe", Path.Combine(dir, "KutuShell.exe")))
            return false;

        using var logo = assembly.GetManifestResourceStream("KutuMark.png");
        if (logo is null)
            return false;

        var logoBytes = ReadAll(logo);
        File.WriteAllBytes(Path.Combine(dir, "StoreLogo.png"), logoBytes);
        File.WriteAllBytes(Path.Combine(dir, "Square44x44Logo.png"), logoBytes);
        File.WriteAllBytes(Path.Combine(dir, "Square150x150Logo.png"), logoBytes);
        return true;
    }

    private static string ShellDirectory() => ShellLayout.VersionFolder(SettingsService.AppDirectory, VersionLabel());

    private static string VersionLabel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 1, 0, 0);
        return version.ToString(3);
    }

    private static string PackageVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 1, 0, 0);
        return version.ToString(4);
    }

    private static string PayloadId()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var dll = assembly.GetManifestResourceStream("KutuShell.dll");
        using var host = assembly.GetManifestResourceStream("KutuShell.exe");
        if (dll is null || host is null)
            return "missing";

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, dll);
        Append(hash, host);
        return ToHex(hash.GetHashAndReset());
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

    private static bool RunInstaller(string manifest, string dir)
    {
        var script = Path.Combine(dir, "kutu-shell.ps1");
        var manifestArg = manifest.Replace("'", "''");
        var dirArg = dir.Replace("'", "''");
        File.WriteAllText(script, $$"""
            $ErrorActionPreference = 'Stop'
            Get-AppxPackage -Name '{{PackageName}}' | Remove-AppxPackage -ErrorAction SilentlyContinue
            try {
              Add-AppxPackage -Path '{{manifestArg}}' -ExternalLocation '{{dirArg}}' -AllowUnsigned
            } catch {
              try {
                Add-AppxPackage -Path '{{manifestArg}}' -ExternalLocation '{{dirArg}}'
              } catch {
                [Console]::Error.WriteLine(($_ | Out-String))
                exit 1
              }
            }
            """);
        return RunPowerShell(script, Path.Combine(dir, "kutu-shell.log"));
    }

    private static void TryRemoveSparse()
    {
        var script = Path.Combine(Path.GetTempPath(), "kutu-shell-remove.ps1");
        File.WriteAllText(script, $"Get-AppxPackage -Name '{PackageName}' | Remove-AppxPackage -ErrorAction SilentlyContinue");
        RunPowerShell(script, Path.Combine(Path.GetTempPath(), "kutu-shell-remove.log"));
    }

    private static bool RunPowerShell(string script, string logPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null)
                return false;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(45000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // The installer is already gone.
                }

                return false;
            }

            var text = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                try
                {
                    File.WriteAllText(logPath, string.IsNullOrWhiteSpace(text) ? "exit " + process.ExitCode : text);
                }
                catch
                {
                    // The stamp still records classic-only.
                }
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Manifest()
    {
        var items = string.Join("\n", Extensions.Select(ext =>
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
              <Identity Name="{PackageName}" Publisher="CN=Kerim Eksi, O=Kutu, C=TR" Version="{PackageVersion()}" />
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
                <Application Id="Kutu" Executable="KutuShell.exe" uap10:TrustLevel="mediumIL" uap10:RuntimeBehavior="win32App">
                  <uap:VisualElements DisplayName="Kutu" Description="Kutu" BackgroundColor="transparent" Square150x150Logo="Square150x150Logo.png" Square44x44Logo="Square44x44Logo.png" AppListEntry="none" />
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
                </Application>
              </Applications>
            </Package>
            """;
    }

    private static bool WriteResource(Assembly assembly, string name, string path)
    {
        using var source = assembly.GetManifestResourceStream(name);
        if (source is null)
            return false;

        using var file = File.Create(path);
        source.CopyTo(file);
        return true;
    }

    private static byte[] ReadAll(Stream source)
    {
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        return memory.ToArray();
    }

    private static void Append(IncrementalHash hash, Stream source)
    {
        var buffer = new byte[8192];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            hash.AppendData(buffer, 0, read);
    }

    private static string ToHex(byte[] bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            chars[i * 2] = Hex(value >> 4);
            chars[i * 2 + 1] = Hex(value & 0xF);
        }

        return new string(chars);
    }

    private static char Hex(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);

    private static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}

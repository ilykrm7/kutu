using System.Text;
using Kutu.Core.Geometry;
using Kutu.Core.Security;
using Kutu.Core.Hotkeys;
using Kutu.Core.IO;
using Kutu.Core.Models;
using Kutu.Core.Network;
using Kutu.Core.Settings;
using Kutu.Core.Text;
using Kutu.Core.Updates;

namespace Kutu.Core.Tests;

public class V11LogicTests
{
    [Fact]
    public void Turkish_letters_and_words_go_to_english()
    {
        Assert.Equal("EN", TranslateDirection.InitialTarget("ağaç"));
        Assert.Equal("EN", TranslateDirection.InitialTarget("ÇĞİÖŞÜ"));
        Assert.Equal("EN", TranslateDirection.InitialTarget("merhaba dünya"));
        Assert.Equal("EN", TranslateDirection.InitialTarget("Bu bir deneme"));
        Assert.True(TranslateDirection.LooksTurkish("ı"));
    }

    [Fact]
    public void Other_text_starts_toward_turkish_and_retries_when_detected_turkish()
    {
        Assert.Equal("TR", TranslateDirection.InitialTarget("hello world"));
        Assert.Null(TranslateDirection.HeuristicTarget("The quick brown fox"));
        Assert.Equal("EN", TranslateDirection.RetryTarget("TR", "TR"));
        Assert.Equal("EN", TranslateDirection.RetryTarget("tr", "tr-TR"));
        Assert.Null(TranslateDirection.RetryTarget("TR", "EN"));
        Assert.Null(TranslateDirection.RetryTarget("EN", "EN"));
        Assert.Null(TranslateDirection.RetryTarget("TR", null));
        Assert.Equal("DE", TranslateDirection.NormalizeLang("de-DE"));
    }

    [Fact]
    public void Screen_lines_skip_text_already_in_the_target()
    {
        var marked = ScreenLinePlan.Mark(["merhaba", "hello world", "   ", "ağ"], "TR");
        Assert.True(marked[0].Skip);
        Assert.False(marked[1].Skip);
        Assert.True(marked[2].Skip);
        Assert.True(marked[3].Skip);

        var english = ScreenLinePlan.Mark(["hello world", "merhaba"], "EN");
        Assert.True(english[0].Skip);
        Assert.False(english[1].Skip);

        var pending = ScreenLinePlan.Pending(english);
        Assert.Equal(["merhaba"], pending);
        var packed = ScreenLinePlan.Pack(pending, 24);
        Assert.Contains("[[1]] merhaba", packed[0]);
        var unpacked = ScreenLinePlan.Unpack("[[1]] hello\n[[2]] world", 2);
        Assert.Equal("hello", unpacked[0]);
        Assert.Equal("world", unpacked[1]);
        Assert.False(ScreenLinePlan.ShowTranslation("merhaba", ""));
        Assert.False(ScreenLinePlan.ShowTranslation("Merhaba", " merhaba "));
        Assert.True(ScreenLinePlan.ShowTranslation("hello", "merhaba"));
    }

    [Fact]
    public void Uniform_black_frames_are_detected_without_a_desktop()
    {
        var black = new byte[8 * 4];
        Assert.True(FrameCheck.IsUniformBlack(black, 8, 2, 2));
        black[4] = 3;
        Assert.False(FrameCheck.IsUniformBlack(black, 8, 2, 2));
        Assert.True(FrameCheck.IsUniformBlack(null, 8, 2, 2));
    }

    [Fact]
    public void Monitor_layout_scales_each_screen_and_places_neighbors_in_dips()
    {
        var single = MonitorLayout.Arrange([new MonitorLayout.Input(0, 0, 2880, 1620, 144, 144, true)]);
        Assert.Equal(1920, single[0].DipWidth, 0);
        Assert.Equal(1080, single[0].DipHeight, 0);

        var pair = MonitorLayout.Arrange(
        [
            new MonitorLayout.Input(0, 0, 1920, 1080, 96, 96, true),
            new MonitorLayout.Input(1920, 0, 3840, 2160, 192, 192, false)
        ]);
        Assert.Equal(1920, pair[1].DipX, 0);
        Assert.Equal(0, pair[1].DipY, 0);
        Assert.Equal(1920, pair[1].DipWidth, 0);
        Assert.True(MonitorLayout.Matches(pair, 0, 0, 3840, 1080));

        var left = MonitorLayout.Arrange(
        [
            new MonitorLayout.Input(0, 0, 1920, 1080, 144, 144, true),
            new MonitorLayout.Input(-1920, 0, 1920, 1080, 96, 96, false)
        ]);
        Assert.Equal(-1920, left[1].DipX, 0);
        var box = MonitorLayout.UnderLine(100, 40, 200, 20, 0, 0, left);
        Assert.Equal(100 * 96d / 144d, box.X, 1);
        Assert.Equal((40 + 20) * 96d / 144d, box.Y, 1);
    }

    [Fact]
    public void Transfer_status_uses_turkish_units_and_smoothed_speed()
    {
        var received = (long)Math.Round(34.1 * 1024 * 1024);
        var total = (long)Math.Round(80.2 * 1024 * 1024);
        var speed = 5.2 * 1024 * 1024;
        Assert.Equal("34,1 / 80,2 MB · 5,2 MB/sn · kalan 0:09", TransferStatus.Format(received, total, speed));
        Assert.Equal("80 MB", TransferStatus.Size(80L * 1024 * 1024));

        var meter = new TransferMeter();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, meter.Observe(0, start));
        var smoothed = meter.Observe(1024 * 1024, start.AddSeconds(1));
        Assert.InRange(smoothed, 900_000, 1_200_000);
        var held = meter.Observe(1024 * 1024 + 1000, start.AddMilliseconds(1100));
        Assert.Equal(smoothed, held);
    }

    [Fact]
    public void Tool_hotkeys_keep_existing_defaults_and_move_color_off_password()
    {
        var fresh = new AppSettings();
        Assert.Equal("Ctrl+Shift+S", ToolHotkeyMap.Get(fresh, "screenshot"));
        Assert.Equal("Ctrl+Alt+T", ToolHotkeyMap.Get(fresh, "translate"));
        Assert.Equal("Ctrl+Alt+E", ToolHotkeyMap.Get(fresh, "screen-translate"));
        Assert.Equal("Ctrl+Alt+R", ToolHotkeyMap.Get(fresh, "screen-record"));
        Assert.Equal("Ctrl+Alt+V", ToolHotkeyMap.Get(fresh, "clipboard"));
        Assert.Equal("Ctrl+Alt+O", ToolHotkeyMap.Get(fresh, "ocr"));
        Assert.Equal("Ctrl+Alt+C", ToolHotkeyMap.Get(fresh, "color-picker"));
        Assert.Equal("Ctrl+Alt+Q", ToolHotkeyMap.Get(fresh, "qr"));
        Assert.Equal("Ctrl+Alt+P", ToolHotkeyMap.Get(fresh, "password"));
        Assert.Equal("", ToolHotkeyMap.Get(fresh, "whois"));
        Assert.Equal("Alt+Space", fresh.LauncherHotkey);

        var legacy = new SettingsDocument
        {
            ToolHotkeys = null,
            ScreenshotHotkey = "Ctrl+Shift+S",
            ColorPickerHotkey = "Ctrl+Alt+P",
            ClipboardHotkey = "Ctrl+Alt+V",
            TranslateHotkey = "Ctrl+Alt+T",
            ScreenTranslateHotkey = "Ctrl+Alt+E",
            RecordHotkey = "Ctrl+Alt+R"
        };
        var imported = ToolHotkeyMap.Import(legacy);
        Assert.Equal("Ctrl+Alt+C", imported["color-picker"]);
        Assert.Equal("Ctrl+Alt+P", imported["password"]);
        Assert.Equal("Ctrl+Shift+S", imported["screenshot"]);

        var custom = new SettingsDocument
        {
            ToolHotkeys = null,
            ColorPickerHotkey = "Ctrl+Shift+C",
            ScreenshotHotkey = "Ctrl+Shift+S",
            ClipboardHotkey = "Ctrl+Alt+V",
            TranslateHotkey = "Ctrl+Alt+T"
        };
        var kept = ToolHotkeyMap.Import(custom);
        Assert.Equal("Ctrl+Shift+C", kept["color-picker"]);

        var settings = new AppSettings { ToolHotkeys = kept, LauncherHotkey = "Alt+Space" };
        ToolHotkeyMap.Set(settings, "whois", "Ctrl+Alt+T");
        var conflicts = ToolHotkeyMap.LocalConflicts(settings, ["translate", "whois", "screenshot"]);
        Assert.Contains("whois", conflicts);
        Assert.Contains("translate", conflicts);
        Assert.DoesNotContain("screenshot", conflicts);

        ToolHotkeyMap.Reset(settings, ["screenshot", "whois", "translate", "password"]);
        Assert.Equal("Ctrl+Shift+S", ToolHotkeyMap.Get(settings, "screenshot"));
        Assert.Equal("", ToolHotkeyMap.Get(settings, "whois"));
        Assert.Equal("Ctrl+Alt+P", ToolHotkeyMap.Get(settings, "password"));
        Assert.Equal("Alt+Space", settings.LauncherHotkey);
        Assert.Empty(ToolHotkeyMap.LocalConflicts(settings, ["screenshot", "whois", "translate", "password"]));
    }

    [Fact]
    public async Task Legacy_color_hotkey_moves_off_password_until_the_recorder_saves()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kutu-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, """
        {
          "colorPickerHotkey": "Ctrl+Alt+P",
          "screenshotHotkey": "Ctrl+Shift+S",
          "clipboardHotkey": "Ctrl+Alt+V",
          "translateHotkey": "Ctrl+Alt+T",
          "launcherHotkey": "Alt+Space"
        }
        """);

        var service = new SettingsService(new PlainProtector(), path);
        await service.LoadAsync();
        Assert.Equal("Ctrl+Alt+C", ToolHotkeyMap.Get(service.Current, "color-picker"));
        Assert.Equal("Ctrl+Alt+P", ToolHotkeyMap.Get(service.Current, "password"));

        var persisted = new SettingsService(new PlainProtector(), path);
        await persisted.LoadAsync();
        Assert.Equal("Ctrl+Alt+C", ToolHotkeyMap.Get(persisted.Current, "color-picker"));
        Assert.Equal("Ctrl+Alt+P", ToolHotkeyMap.Get(persisted.Current, "password"));

        ToolHotkeyMap.Set(persisted.Current, "color-picker", "Ctrl+Alt+P");
        service = persisted;
        await service.SaveAsync();
        var again = new SettingsService(new PlainProtector(), path);
        await again.LoadAsync();
        Assert.Equal("Ctrl+Alt+P", ToolHotkeyMap.Get(again.Current, "color-picker"));
        Assert.Equal("Ctrl+Alt+P", ToolHotkeyMap.Get(again.Current, "password"));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Password_uses_requested_classes_and_drops_ambiguous_characters()
    {
        var recipe = new PasswordRecipe(24, true, true, true, true, true);
        var password = PasswordGenerator.Create(recipe);
        Assert.Equal(24, password.Length);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, ch => "!@#$%^&*()-_=+[]{};:,.?".Contains(ch));
        Assert.DoesNotContain(password, ch => "0Oo1lI|".Contains(ch));

        var digits = PasswordGenerator.Create(new PasswordRecipe(12, false, false, true, false, true));
        Assert.Equal(12, digits.Length);
        Assert.All(digits, ch => Assert.Contains(ch, "23456789"));
        Assert.NotEqual(password, PasswordGenerator.Create(recipe));
    }

    [Fact]
    public void Port_presets_lead_with_rdp_and_ssh()
    {
        Assert.True(PortPlan.TryBuild("all", "", out var all, out _));
        Assert.Equal(65535, all.Length);
        Assert.Equal(3389, all[0]);
        Assert.Equal(22, all[1]);
        Assert.Equal(1, all[2]);
        Assert.Equal(21, all[22]);
        Assert.Equal(23, all[23]);
        Assert.DoesNotContain(all.Skip(2), port => port is 22 or 3389);

        Assert.True(PortPlan.TryBuild("common", "", out var common, out _));
        Assert.Equal(3389, common[0]);
        Assert.Equal(22, common[1]);

        Assert.True(PortPlan.TryBuild("range", "20-23", out var range, out _));
        Assert.Equal([22, 20, 21, 23], range);

        Assert.True(PortPlan.TryBuild("range", "80-82", out var plain, out _));
        Assert.Equal([80, 81, 82], plain);

        Assert.True(PortPlan.TryBuild("single", "1453", out var one, out _));
        Assert.Equal([1453], one);
        Assert.True(PortPlan.TryBuild("single", "22", out var ssh, out _));
        Assert.Equal([22], ssh);
        Assert.False(PortPlan.TryBuild("single", "0", out _, out var error));
        Assert.Equal("invalid", error);
    }

    [Fact]
    public void Service_probe_labels_ssh_http_tls_and_rdp()
    {
        Assert.Equal(19, ServiceProbe.RdpConnectionRequest.Length);
        Assert.Equal(0x03, ServiceProbe.RdpConnectionRequest[0]);
        Assert.Equal(0x13, ServiceProbe.RdpConnectionRequest[3]);
        Assert.Equal(0xe0, ServiceProbe.RdpConnectionRequest[5]);
        Assert.Equal(0x01, ServiceProbe.RdpConnectionRequest[11]);

        byte[] confirm =
        [
            0x03, 0x00, 0x00, 0x13, 0x0e, 0xd0, 0x00, 0x00, 0x12, 0x34, 0x00,
            0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        Assert.True(ServiceProbe.IsRdpResponse(confirm));
        Assert.Equal("RDP", ServiceProbe.Classify(confirm));
        Assert.Equal("SSH", ServiceProbe.ClassifyBanner("SSH-2.0-OpenSSH_9.0"));
        Assert.Equal("HTTP", ServiceProbe.ClassifyBanner("HTTP/1.1 200 OK"));
        Assert.Equal("HTTPS (TLS)", ServiceProbe.Classify([0x16, 0x03, 0x03, 0x00, 0x02]));
        Assert.False(ServiceProbe.IsRdpResponse(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK")));
        Assert.Equal(0x16, ServiceProbe.TlsClientHello[0]);
        Assert.Equal(ServiceProbe.TlsClientHello.Length - 5, (ServiceProbe.TlsClientHello[3] << 8) | ServiceProbe.TlsClientHello[4]);
    }

    [Fact]
    public void Ip_list_and_symantec_link_do_not_invent_a_score()
    {
        var ips = IpList.Parse("8.8.8.8, not-an-ip\n1.1.1.1 8.8.8.8;2001:db8::1");
        Assert.Equal(["8.8.8.8", "1.1.1.1", "2001:db8::1"], ips);
        var url = SymantecReview.Url("8.8.8.8");
        Assert.StartsWith("https://sitereview.bluecoat.com/sitereview.jsp?url=", url);
        Assert.Contains("8.8.8.8", url);
        Assert.Contains("no public", SymantecReview.Finding, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Csv_quotes_and_release_version_compares()
    {
        var csv = BulkCsv.Write(
        [
            new BulkIpExport("8.8.8.8", "6", "US", "City, X", "ISP", "15169", "0", "no", "https://example.test/a,b")
        ]);
        Assert.Contains("\"City, X\"", csv);
        Assert.Contains("\"https://example.test/a,b\"", csv);
        Assert.True(ReleaseVersion.IsNewer("v1.2.0", new Version(1, 1, 0)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.1", new Version(1, 1, 0)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.1", new Version(1, 1, 1)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.0", new Version(1, 1, 0)));
        Assert.False(ReleaseVersion.IsNewer("v1.0.9", new Version(1, 1, 0)));
        var release = ReleaseVersion.ParseLatest("""
            {"tag_name":"v1.2.0","assets":[{"name":"Kutu.exe","browser_download_url":"https://example.test/Kutu.exe"},{"name":"Kutu.exe.sha256","browser_download_url":"https://example.test/Kutu.exe.sha256"}]}
            """);
        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release!.Version);
        Assert.Equal("https://example.test/Kutu.exe", ReleaseVersion.Find(release.Assets, "Kutu.exe")!.Url);
    }

    [Fact]
    public void Sha256_file_matches_the_named_entry()
    {
        var payload = Encoding.ASCII.GetBytes("kutu");
        var hash = Sha256Check.Compute(payload);
        var sums = hash + "  yt-dlp.exe\n" + new string('a', 64) + "  other.bin\n";
        Assert.Equal(hash, Sha256Check.FindHash(sums, "yt-dlp.exe"));
        Assert.Null(Sha256Check.FindHash(sums, "missing.exe"));
        var path = Path.Combine(Path.GetTempPath(), "kutu-hash-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, payload);
        try
        {
            Assert.True(Sha256Check.Matches(path, sums, "yt-dlp.exe"));
            Assert.False(Sha256Check.Matches(path, new string('b', 64) + "  yt-dlp.exe\n", "yt-dlp.exe"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class PlainProtector : ISecretProtector
    {
        public string Protect(string plain) => "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));

        public string Unprotect(string protectedValue)
        {
            var payload = protectedValue["enc:".Length..];
            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }
    }
}

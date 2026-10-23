using System.Text;
using System.Text.Json;
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

        byte[] confirmOnly =
        [
            0x03, 0x00, 0x00, 0x13, 0x0e, 0xd0, 0x00, 0x00, 0x12, 0x34, 0x00,
            0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00
        ];
        Assert.Equal("RDP", ServiceProbe.Identify([], confirmOnly));
        Assert.Equal("SSH", ServiceProbe.Identify(Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.0")));
        Assert.Equal("SSH", ServiceProbe.Identify([], null, Encoding.ASCII.GetBytes("SSH-2.0-OpenSSH_9.0")));
    }

    [Fact]
    public void Port_scan_dedupes_and_retries_only_unanswered_ports()
    {
        var table = new OpenPortTable();
        Assert.True(table.Add(139));
        Assert.False(table.Add(139));
        Assert.True(table.Add(1453));
        Assert.Equal(2, table.Count);
        Assert.Equal([139, 1453], table.Ports);
        Assert.True(PortPlan.TryBuild("common", "", out var common, out _));
        Assert.Equal(common.Length, common.Distinct().Count());
        Assert.Equal(1, common.Count(port => port == 139));

        Assert.Equal(64, PortScanBudget.Concurrency(512));
        Assert.Equal(48, PortScanBudget.Concurrency(48));
        Assert.Equal(250, PortScanBudget.Timeout(200));
        Assert.Equal(400, PortScanBudget.Timeout(400));
        Assert.True(PortScanBudget.ShouldRetry(PortAnswer.Unanswered, 0));
        Assert.False(PortScanBudget.ShouldRetry(PortAnswer.Unanswered, 1));
        Assert.False(PortScanBudget.ShouldRetry(PortAnswer.Refused, 0));
        Assert.False(PortScanBudget.ShouldRetry(PortAnswer.Open, 0));
        Assert.True(PortScanBudget.RetryTimeout(400) >= 800);
    }

    [Fact]
    public void Certificate_text_uses_tls_names_and_local_dates()
    {
        Assert.Equal("TLS 1.3", CertificateText.Protocol("Tls13"));
        Assert.Equal("TLS 1.2", CertificateText.Protocol("Tls12"));
        Assert.Equal("TLS 1.0", CertificateText.Protocol("Tls"));
        Assert.Equal("05.03.2026 15:04", CertificateText.Stamp(new DateTime(2026, 3, 5, 15, 4, 0, DateTimeKind.Unspecified)));
        Assert.Matches(@"^\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}$", CertificateText.Stamp(DateTime.UtcNow));
        Assert.DoesNotContain("Z", CertificateText.Stamp(DateTime.UtcNow));
    }

    [Fact]
    public void Domain_lookup_uses_ipv4_resolvers_and_hides_raw_dns_errors()
    {
        Assert.Equal(["1.1.1.1", "1.0.0.1", "8.8.8.8", "8.8.4.4"], PublicDns.IPv4);
        Assert.All(PublicDns.IPv4, ip => Assert.DoesNotContain(":", ip));
        var urls = RdapUrls.For("kerimeksi.com");
        Assert.Contains("https://rdap.org/domain/kerimeksi.com", urls);
        Assert.Contains("https://rdap.verisign.com/com/v1/domain/kerimeksi.com", urls);
        Assert.Contains("https://www.rdap.net/domain/kerimeksi.com", urls);
        Assert.True(urls.Count >= 2);

        var raw = "Query 4324 => kerimeksi.com IN AAAA on [2a02:8108:0:1::1]:53 timed out or is a transient error";
        Assert.Equal("dns", LookupFailure.Kind(new InvalidOperationException(raw)));
        Assert.Equal("timeout", LookupFailure.Kind(new TimeoutException("The operation has timed out.")));
        Assert.DoesNotContain("2a02", LookupFailure.Kind(new InvalidOperationException(raw)));
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
            new BulkIpExport("8.8.8.8", "6", "US", "City, X", "ISP", "15169", "0", "no", "87", "high", "https://example.test/a,b")
        ]);
        Assert.StartsWith("ip,risk,country,city,isp,asn,dnsbl,tor,scamalytics_score,scamalytics_risk,symantec_url", csv);
        Assert.Contains(",87,high,", csv);
        Assert.Contains("\"City, X\"", csv);
        Assert.Contains("\"https://example.test/a,b\"", csv);
        Assert.Equal("87 yüksek", ScamalyticsApi.Cell(87, "high"));
        Assert.Equal("", ScamalyticsApi.Cell(null, null));
        Assert.True(ReleaseVersion.IsNewer("v1.2.0", new Version(1, 1, 0)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.1", new Version(1, 1, 0)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.1", new Version(1, 1, 1)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.0", new Version(1, 1, 0)));
        Assert.False(ReleaseVersion.IsNewer("v1.0.9", new Version(1, 1, 0)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.2", new Version(1, 1, 0)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.2", new Version(1, 1, 1)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.2", new Version(1, 1, 2)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.3", new Version(1, 1, 0)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.3", new Version(1, 1, 1)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.3", new Version(1, 1, 2)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.3", new Version(1, 1, 3)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.4", new Version(1, 1, 0)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.4", new Version(1, 1, 1)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.4", new Version(1, 1, 2)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.4", new Version(1, 1, 3)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.4", new Version(1, 1, 4)));
        var patched = ReleaseVersion.ParseLatest("""
            {"tag_name":"v1.1.2","assets":[{"name":"Kutu.zip","browser_download_url":"https://example.test/Kutu.zip"},{"name":"Kutu.exe","browser_download_url":"https://example.test/Kutu.exe"},{"name":"Kutu.exe.sha256","browser_download_url":"https://example.test/Kutu.exe.sha256"}]}
            """);
        Assert.Equal("v1.1.2", patched!.Version);
        Assert.Equal("https://example.test/Kutu.exe", ReleaseVersion.Find(patched.Assets, "Kutu.exe")!.Url);
        Assert.Equal("https://example.test/Kutu.exe.sha256", ReleaseVersion.Find(patched.Assets, "Kutu.exe.sha256")!.Url);
        var sums = Sha256Check.Compute(Encoding.ASCII.GetBytes("release")) + "  Kutu.exe\n";
        Assert.Equal(Sha256Check.Compute(Encoding.ASCII.GetBytes("release")), Sha256Check.FindHash(sums, "Kutu.exe"));
        var release = ReleaseVersion.ParseLatest("""
            {"tag_name":"v1.2.0","assets":[{"name":"Kutu.exe","browser_download_url":"https://example.test/Kutu.exe"},{"name":"Kutu.exe.sha256","browser_download_url":"https://example.test/Kutu.exe.sha256"}]}
            """);
        Assert.NotNull(release);
        Assert.Equal("v1.2.0", release!.Version);
        Assert.Equal("https://example.test/Kutu.exe", ReleaseVersion.Find(release.Assets, "Kutu.exe")!.Url);
        var current = ReleaseVersion.ParseLatest("""
            {"tag_name":"v1.1.3","assets":[{"name":"Kutu.zip","browser_download_url":"https://example.test/Kutu.zip"},{"name":"Kutu.exe","browser_download_url":"https://example.test/v113/Kutu.exe"},{"name":"Kutu.exe.sha256","browser_download_url":"https://example.test/v113/Kutu.exe.sha256"}]}
            """);
        Assert.Equal("v1.1.3", current!.Version);
        Assert.Equal("https://example.test/v113/Kutu.exe", ReleaseVersion.Find(current.Assets, "Kutu.exe")!.Url);
        Assert.Equal("https://example.test/v113/Kutu.exe.sha256", ReleaseVersion.Find(current.Assets, "Kutu.exe.sha256")!.Url);
    }

    [Fact]
    public void Scamalytics_reads_the_official_score_and_ignores_the_website()
    {
        Assert.False(ScamalyticsApi.HasCredentials("", "key"));
        Assert.False(ScamalyticsApi.HasCredentials("user", " "));
        Assert.False(ScamalyticsApi.HasCredentials(null, null));
        Assert.True(ScamalyticsApi.HasCredentials("kerim", "secret"));
        var url = ScamalyticsApi.Url("kerim", "secret key", "8.8.8.8");
        Assert.StartsWith("https://api12.scamalytics.com/v3/kerim?key=secret%20key&ip=8.8.8.8", url);
        Assert.DoesNotContain("scamalytics.com/ip", url);
        using var doc = JsonDocument.Parse("""
            {"scamalytics":{"status":"ok","scamalytics_score":87,"scamalytics_risk":"high","scamalytics_proxy":{"is_datacenter":true,"is_vpn":false,"is_resproxy":null},"scamalytics_url":"https://scamalytics.com/ip/8.8.8.8"}}
            """);
        var reading = ScamalyticsApi.Read(doc.RootElement);
        Assert.True(reading.Ok);
        Assert.Equal(87, reading.Score);
        Assert.Equal("high", reading.Risk);
        Assert.True(reading.Datacenter);
        Assert.False(reading.Vpn);
        Assert.Equal("", reading.Error);
        Assert.True(ScamalyticsApi.IsRisk(reading.Score, reading.Risk));
        Assert.Equal("yüksek", ScamalyticsApi.RiskLabel("high"));
        Assert.Equal("çok yüksek", ScamalyticsApi.RiskLabel("very high"));
        Assert.Equal("düşük", ScamalyticsApi.RiskLabel("low"));
        Assert.Equal("orta", ScamalyticsApi.RiskLabel("medium"));
        using var low = JsonDocument.Parse("""{"scamalytics":{"status":"ok","scamalytics_score":0,"scamalytics_risk":"low"}}""");
        var clean = ScamalyticsApi.Read(low.RootElement);
        Assert.False(ScamalyticsApi.IsRisk(clean.Score, clean.Risk));
        Assert.Null(clean.Vpn);
        Assert.Null(clean.Datacenter);
        using var bad = JsonDocument.Parse("""{"scamalytics":{"status":"error","error":"Unauthorized: invalid API key secret-value"}}""");
        var failed = ScamalyticsApi.Read(bad.RootElement);
        Assert.False(failed.Ok);
        Assert.Equal("Anahtar kabul edilmedi", failed.Error);
        Assert.DoesNotContain("secret-value", failed.Error);
        using var other = JsonDocument.Parse("""{"scamalytics":{"status":"error","error":"Excess limit: credits exhausted"}}""");
        Assert.Equal("Sorgu tamamlanamadı", ScamalyticsApi.Read(other.RootElement).Error);
        using var missing = JsonDocument.Parse("""{"external_datasources":{}}""");
        Assert.Equal("Ulaşılamadı", ScamalyticsApi.Read(missing.RootElement).Error);
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

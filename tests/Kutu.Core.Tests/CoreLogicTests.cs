using System.Runtime.InteropServices;
using Kutu.Core;
using Kutu.Core.Color;
using Kutu.Core.Geometry;
using Kutu.Core.Commands;
using Kutu.Core.Hotkeys;
using Kutu.Core.Models;
using Kutu.Core.Network;
using Kutu.Core.Security;
using Kutu.Core.Settings;
using Kutu.Core.Text;
using Kutu.Imaging;

namespace Kutu.Core.Tests;

public class CoreLogicTests
{
    [Fact]
    public void Color_roundtrip_and_hsl()
    {
        Assert.True(RgbColor.TryParseHex("#28A0F1", out var color));
        Assert.Equal(0x28, color.R);
        Assert.Equal(0xA0, color.G);
        Assert.Equal(0xF1, color.B);
        Assert.Equal("#28A0F1", color.Hex);
        Assert.Equal("rgb(40, 160, 241)", color.Rgb);
        var (h, s, l) = color.ToHsl();
        Assert.InRange(h, 190, 210);
        Assert.True(s > 70);
        Assert.InRange(l, 40, 70);
        Assert.False(RgbColor.TryParseHex("nope", out _));
    }

    [Fact]
    public void Snippet_matches_longest_trigger_after_boundary()
    {
        var snippets = new[]
        {
            new Snippet { Trigger = ":im", Body = "short" },
            new Snippet { Trigger = ":imza", Body = "Kerim" },
            new Snippet { Trigger = ":paste", Body = "hello" }
        };

        Assert.Equal("Kerim", SnippetMatcher.Match("merhaba :imza", snippets)?.Body);
        Assert.Equal("hello", SnippetMatcher.Match(":paste", snippets)?.Body);
        Assert.Null(SnippetMatcher.Match(":imz", snippets));
        Assert.Equal("Kerim", SnippetMatcher.Match("mail:imza", snippets)?.Body);
        Assert.Equal("administrator", SnippetMatcher.Match("xx:adm", [new Snippet { Trigger = ":adm", Body = "administrator" }])?.Body);
    }

    [Fact]
    public void Snippet_expands_colon_adm_without_trailing_space()
    {
        var snippets = new[] { new Snippet { Trigger = ":adm", Body = "administrator" } };
        Assert.Equal("administrator", SnippetMatcher.Match(":adm", snippets)?.Body);
        Assert.Null(SnippetMatcher.Match(":ad", snippets));
        Assert.Equal("administrator", SnippetMatcher.Match("xx:adm", snippets)?.Body);
    }

    [Fact]
    public void Expander_resets_on_enter_then_matches_colon_adm()
    {
        var buffer = new ExpanderBuffer();
        var snippets = new[] { new Snippet { Trigger = ":adm", Body = "administrator" } };
        buffer.Type("r", snippets, win: true);
        Assert.Equal("", buffer.Text);
        buffer.Type("notepad", snippets);
        Assert.Equal("notepad", buffer.Text);
        buffer.Enter();
        buffer.FocusChanged();
        Assert.Equal("", buffer.Text);
        Assert.Null(buffer.Type(":", snippets));
        Assert.Equal(":", buffer.Text);
        Assert.Null(buffer.Type("a", snippets));
        Assert.Null(buffer.Type("d", snippets));
        var match = buffer.Type("m", snippets);
        Assert.Equal("administrator", match?.Body);
        Assert.Equal(":adm", buffer.Text);
        buffer.MouseClick();
        Assert.Equal("", buffer.Text);
        buffer.Type("keep", snippets);
        buffer.FocusChanged();
        Assert.Equal("", buffer.Text);
        buffer.Tab();
        buffer.Escape();
        Assert.Equal("", buffer.Text);
    }

    [Fact]
    public void Expander_log_shows_only_the_trigger_suffix()
    {
        Assert.Null(ExpanderLogText.VisibleBuffer("notepad"));
        Assert.Equal(":adm", ExpanderLogText.VisibleBuffer("notepad:adm"));
        Assert.Equal(":", ExpanderLogText.VisibleBuffer(":"));
    }

    [Fact]
    public void Selection_keeps_same_stem_with_different_extensions()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kutu-pick", Guid.NewGuid().ToString("N"));
        var selected = ImageFileList.CombineSelection([dir, "photo.jpg", "photo.png", "photo.webp"]);
        Assert.Equal(
            [
                Path.GetFullPath(Path.Combine(dir, "photo.jpg")),
                Path.GetFullPath(Path.Combine(dir, "photo.png")),
                Path.GetFullPath(Path.Combine(dir, "photo.webp"))
            ],
            selected);
        var list = new List<string>();
        ImageFileList.AddDistinct(list, selected);
        ImageFileList.AddDistinct(list, [selected[0]]);
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void Convert_to_is_headless_and_keeps_every_path()
    {
        var args = new[] { "--convert-to", "jpeg", @"C:\photos\photo.jpg", @"C:\photos\photo.png", @"C:\photos\photo.webp" };
        Assert.True(ConvertArgs.IsHeadless(args));
        Assert.False(ConvertArgs.IsHeadless(["--screenshot"]));
        Assert.True(ConvertArgs.TryParse(args, out var format, out var files));
        Assert.Equal("jpeg", format);
        Assert.Equal(3, files.Count);
        Assert.Equal(@"C:\photos\photo.webp", files[2]);
    }

    [Fact]
    public void Expander_accepts_remote_injected_keys()
    {
        Assert.True(ExpanderHook.ShouldHandle(0x10, injecting: false));
        Assert.True(ExpanderHook.ShouldHandle(0x02, injecting: false));
        Assert.True(ExpanderHook.ShouldHandle(0, injecting: false));
        Assert.False(ExpanderHook.ShouldHandle(0x10, injecting: true));
        Assert.False(ExpanderHook.ShouldHandle(0, injecting: true));
        Assert.False(ExpanderHook.ShouldHandle(0, injecting: false, ExpanderHook.InjectionMarker));
    }

    [Fact]
    public void Replacement_deletes_the_trigger_then_types_unicode()
    {
        var strokes = KeyStrokePlan.Replace(4, "administrator");
        Assert.Equal((4 * 2) + ("administrator".Length * 2), strokes.Length);
        Assert.All(strokes, stroke => Assert.Equal(ExpanderHook.InjectionMarker, stroke.Extra));
        Assert.Equal((ushort)0x08, strokes[0].Vk);
        Assert.Equal(0u, strokes[0].Flags);
        Assert.Equal(KeyStrokePlan.KeyUp, strokes[1].Flags);
        var typed = strokes[8];
        Assert.Equal((ushort)'a', typed.Scan);
        Assert.Equal(0, typed.Vk);
        Assert.Equal(KeyStrokePlan.UnicodeFlag, typed.Flags);
        Assert.Equal(KeyStrokePlan.UnicodeFlag | KeyStrokePlan.KeyUp, strokes[9].Flags);
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<NativeInput>());
    }

    [Fact]
    public void Hotkey_parse_roundtrip()
    {
        Assert.True(HotkeyGesture.TryParse("Ctrl+Shift+S", out var gesture));
        Assert.True(gesture.Control);
        Assert.True(gesture.Shift);
        Assert.False(gesture.Alt);
        Assert.Equal("S", gesture.Key);
        Assert.Equal("Ctrl+Shift+S", gesture.ToString());
        Assert.True(HotkeyGesture.TryParse("Alt+Space", out var launcher));
        Assert.Equal("Space", launcher.Key);
        Assert.False(HotkeyGesture.TryParse("Ctrl+", out _));
    }

    [Fact]
    public void Domain_strips_url_parts()
    {
        Assert.Equal("ornek.com", DomainName.Normalize("https://ornek.com/path?q=1"));
        Assert.Equal("ornek.com", DomainName.Normalize("ORNEK.com."));
        Assert.True(DomainName.IsComplete("https://ornek.com/path"));
        Assert.True(DomainName.IsComplete("sub.example.co.uk"));
        Assert.False(DomainName.IsComplete("example"));
        Assert.False(DomainName.IsComplete("example.c"));
        Assert.False(DomainName.IsComplete("not a domain"));
        Assert.False(DomainName.IsComplete("192.168.0.1"));
    }

    [Fact]
    public void Rdap_parser_reads_registrar_dates_and_nameservers()
    {
        const string json = """
        {
          "ldhName": "EXAMPLE.COM",
          "status": ["active"],
          "events": [
            {"eventAction": "registration", "eventDate": "1995-08-14T04:00:00Z"},
            {"eventAction": "expiration", "eventDate": "2026-08-13T04:00:00Z"},
            {"eventAction": "last changed", "eventDate": "2024-01-01T00:00:00Z"}
          ],
          "nameservers": [{"ldhName": "NS1.EXAMPLE.COM"}],
          "entities": [{
            "roles": ["registrar"],
            "vcardArray": ["vcard", [
              ["version", {}, "text", "4.0"],
              ["fn", {}, "text", "Example Registrar"]
            ]]
          }]
        }
        """;

        var summary = RdapParser.Parse(json);
        Assert.Equal("EXAMPLE.COM", summary.Domain);
        Assert.Equal("Example Registrar", summary.Registrar);
        Assert.Equal("1995-08-14T04:00:00Z", summary.Registered);
        Assert.Equal("2026-08-13T04:00:00Z", summary.Expires);
        Assert.Equal("2024-01-01T00:00:00Z", summary.Updated);
        Assert.Contains("active", summary.Status);
        Assert.Contains("NS1.EXAMPLE.COM", summary.NameServers);
    }

    [Fact]
    public async Task Settings_encrypts_secrets_and_keeps_snippets()
    {
        var path = Path.Combine(Path.GetTempPath(), "kutu-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var service = new SettingsService(new TestProtector(), path);
        service.Current.DeepLKey = "secret-key";
        service.Current.AbuseIpDbKey = "abuse";
        service.Current.VirusTotalKey = "vt-secret";
        service.Current.UploadApiKey = "upload";
        service.Current.Snippets.Add(new Snippet { Trigger = ":imza", Body = "Kutu" });
        await service.SaveAsync();

        var raw = File.ReadAllText(path);
        Assert.DoesNotContain("secret-key", raw);
        Assert.Contains("enc:", raw);

        var loaded = new SettingsService(new TestProtector(), path);
        await loaded.LoadAsync();
        Assert.Equal("secret-key", loaded.Current.DeepLKey);
        Assert.Equal("abuse", loaded.Current.AbuseIpDbKey);
        Assert.Equal("vt-secret", loaded.Current.VirusTotalKey);
        Assert.Equal("upload", loaded.Current.UploadApiKey);
        Assert.Equal(":imza", loaded.Current.Snippets[0].Trigger);
    }

    [Fact]
    public async Task Settings_marks_unconfirmed_context_menu_for_persist()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kutu-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "settings.json");
        var missing = new SettingsService(new TestProtector(), path);
        await missing.LoadAsync();
        Assert.True(missing.Current.ContextMenuRegistered);
        Assert.True(missing.ContextMenuDirty);
        Assert.False(missing.ContextMenuExplicitOff);

        Directory.CreateDirectory(dir);
        File.WriteAllText(path, """{"contextMenuRegistered":false}""");
        var unset = new SettingsService(new TestProtector(), path);
        await unset.LoadAsync();
        Assert.True(unset.Current.ContextMenuRegistered);
        Assert.True(unset.ContextMenuDirty);

        File.WriteAllText(path, """{"contextMenuRegistered":false,"contextMenuUserSet":true}""");
        var off = new SettingsService(new TestProtector(), path);
        await off.LoadAsync();
        Assert.False(off.Current.ContextMenuRegistered);
        Assert.True(off.ContextMenuExplicitOff);
        Assert.False(off.ContextMenuDirty);
        off.CorrectContextMenuOn();
        Assert.True(off.ContextMenuDirty);
        await off.SaveAsync();
        var raw = File.ReadAllText(path);
        Assert.Contains("\"contextMenuRegistered\": true", raw);

        File.WriteAllText(path, "");
        var broken = new SettingsService(new TestProtector(), path);
        await broken.LoadAsync();
        Assert.True(broken.Current.ContextMenuRegistered);
        Assert.True(broken.ContextMenuDirty);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Ip_risk_weights_the_strongest_signal()
    {
        Assert.Equal("high", IpRisk.Level(IpRisk.Combine([80], 0, false)));
        Assert.Equal("low", IpRisk.Level(IpRisk.Combine([], 0, false)));
        Assert.Null(IpRisk.Combine([], null, null));
        Assert.Equal("1.2.3.4".Replace("1.2.3.4", "4.3.2.1"), Dnsbl.Reverse("1.2.3.4"));
        Assert.Equal(DnsblMark.Listed, Dnsbl.Classify(System.Net.IPAddress.Parse("127.0.0.2")));
        Assert.Equal(DnsblMark.Blocked, Dnsbl.Classify(System.Net.IPAddress.Parse("127.255.255.254")));
        Assert.Equal(DnsblMark.Clean, Dnsbl.Classify(System.Net.IPAddress.Parse("8.8.8.8")));
    }

    [Fact]
    public void Webp_fallback_encoder_writes_riff()
    {
        if (!HostProcess.IsWindows)
            return;

        var dir = Path.Combine(Path.GetTempPath(), "kutu-webp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var pixels = new byte[8 * 8 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xF1;
            pixels[i + 1] = 0xA0;
            pixels[i + 2] = 0x28;
            pixels[i + 3] = 255;
        }

        var path = Path.Combine(dir, "solid.webp");
        LibWebp.Write(pixels, 8, 8, 32, 70, path);
        var encoded = File.ReadAllBytes(path);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(Take(encoded, 4)));
        var decoded = LibWebp.DecodeBgra(encoded, out var width, out var height);
        Assert.Equal(8, width);
        Assert.Equal(8, height);
        Assert.Equal(8 * 8 * 4, decoded.Length);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Drag_box_keeps_every_direction_inside_the_selection()
    {
        var downRight = ShotDrag.FromPoints(10, 20, 80, 50);
        Assert.Equal(new ShotDrag(10, 20, 70, 30), downRight);
        var upLeft = ShotDrag.FromPoints(100, 80, 20, 10);
        Assert.Equal(new ShotDrag(20, 10, 80, 70), upLeft);
        var clamped = upLeft.Clamp(60, 40);
        Assert.Equal(new ShotDrag(20, 10, 40, 30), clamped);
        var point = ShotDrag.ClampPoint(-4, 90, 60, 40);
        Assert.Equal((0d, 40d), point);
        Assert.False(ShotDrag.FromPoints(5, 5, 6, 5).HasExtent());
        Assert.True(downRight.HasExtent());
    }

    [Fact]
    public void Toolbar_stays_inside_a_narrow_monitor()
    {
        var limit = ShotBarSlot.WidthLimit(856, chrome: 18);
        Assert.Equal(822, limit);
        var below = ShotBarSlot.Place(40, 80, 500, 120, limit, 40, 0, 0, 856, 480);
        Assert.Equal(new ShotBarSlot(26, 210), below);
        Assert.True(below.X >= ShotBarSlot.Margin);
        Assert.True(below.X + limit <= 856 - ShotBarSlot.Margin);

        var flipped = ShotBarSlot.Place(40, 360, 400, 100, 700, 44, 0, 0, 856, 480);
        Assert.Equal(new ShotBarSlot(40, 306), flipped);

        var shifted = ShotBarSlot.Place(600, 40, 200, 80, 400, 44, 0, 0, 856, 480);
        Assert.Equal(new ShotBarSlot(448, 130), shifted);

        var inside = ShotBarSlot.Place(0, 0, 856, 480, 800, 100, 0, 0, 856, 480);
        Assert.Equal(new ShotBarSlot(8, 370), inside);

        var other = ShotBarSlot.Place(2600, 40, 100, 80, 500, 44, 1920, 0, 856, 480);
        Assert.Equal(new ShotBarSlot(2268, 130), other);
        Assert.True(other.X >= 1920 + ShotBarSlot.Margin);
        Assert.True(other.X + 500 <= 1920 + 856 - ShotBarSlot.Margin);
    }

    [Fact]
    public void Output_names_keep_the_source_base_name()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kutu-names", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var bmp = Path.Combine(dir, "test.kutu.bmp");
        File.WriteAllBytes(bmp, [1]);
        var first = ImageFormats.OutputPath(bmp, KutuImageFormat.Jpeg);
        Assert.Equal(Path.Combine(dir, "test.kutu.jpg"), first);
        var png = Path.Combine(dir, "test.kutu.png");
        File.WriteAllBytes(png, [1]);
        var second = ImageFormats.OutputPath(png, KutuImageFormat.Jpeg);
        Assert.Equal(Path.Combine(dir, "test.kutu-2.jpg"), second);
        var same = ImageFormats.OutputPath(first, KutuImageFormat.Jpeg);
        Assert.Equal(Path.Combine(dir, "test.kutu-3.jpg"), same);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Pdf_wraps_jpeg_bytes()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var pdf = PdfImage.FromJpeg(jpeg, 40, 20);
        var text = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        var raw = System.Text.Encoding.GetEncoding(28591).GetString(pdf);
        Assert.Contains("DCTDecode", raw);
        Assert.Contains("\u00ff\u00d8\u00ff\u00d9", raw);
    }

    [Fact]
    public async Task Image_pipeline_converts_resizes_and_writes_ico()
    {
        if (!HostProcess.IsWindows)
            return;

        var dir = Path.Combine(Path.GetTempPath(), "kutu-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "in.png");
        File.WriteAllBytes(source, SolidPng(40, 20, 0x28, 0xA0, 0xF1));

        var jpg = Path.Combine(dir, "out.jpg");
        await ImagePipeline.ConvertAsync(source, jpg, KutuImageFormat.Jpeg, 80);
        var webp = Path.Combine(dir, "out.webp");
        await ImagePipeline.ConvertAsync(source, webp, KutuImageFormat.Webp, 70);
        var fromWebp = Path.Combine(dir, "from-webp.png");
        await ImagePipeline.ConvertAsync(webp, fromWebp, KutuImageFormat.Png, 90);

        var ico = Path.Combine(dir, "out.ico");
        await ImagePipeline.ConvertAsync(source, ico, KutuImageFormat.Ico, 90);
        var pdf = Path.Combine(dir, "out.pdf");
        await ImagePipeline.ConvertAsync(source, pdf, KutuImageFormat.Pdf, 80);
        var resized = Path.Combine(dir, "small.png");
        await ImagePipeline.ResizeAndSaveAsync(source, resized, FitMode.LongEdge, 10, KutuImageFormat.Png, 90);

        Assert.True(new FileInfo(jpg).Length > 0);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, Take(File.ReadAllBytes(jpg), 2));
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(Take(File.ReadAllBytes(webp), 4)));
        Assert.Equal(new byte[] { 137, 80, 78, 71 }, Take(File.ReadAllBytes(fromWebp), 4));
        Assert.Equal(0, File.ReadAllBytes(ico)[0]);
        Assert.Equal(1, File.ReadAllBytes(ico)[2]);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(Take(File.ReadAllBytes(pdf), 4)));
        var (w, h) = PngSize(File.ReadAllBytes(resized));
        Assert.Equal(10, w);
        Assert.Equal(5, h);
    }

    private static byte[] Take(byte[] bytes, int count)
    {
        var slice = new byte[count];
        Buffer.BlockCopy(bytes, 0, slice, 0, count);
        return slice;
    }

    private static byte[] SolidPng(int width, int height, byte r, byte g, byte b)
    {
        var raw = new byte[height * (1 + (width * 3))];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + (width * 3));
            raw[row] = 0;
            for (var x = 0; x < width; x++)
            {
                raw[row + 1 + (x * 3)] = r;
                raw[row + 2 + (x * 3)] = g;
                raw[row + 3 + (x * 3)] = b;
            }
        }

        var idat = Zlib(raw);
        using var png = new MemoryStream();
        Write(png, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(png, "IHDR",
        [
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8, 2, 0, 0, 0
        ]);
        WriteChunk(png, "IDAT", idat);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        Write(stream, U32Be(data.Length));
        Write(stream, typeBytes);
        Write(stream, data);
        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        Write(stream, U32Be(Crc32(crcInput)));
    }

    private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);

    private static byte[] Zlib(byte[] raw)
    {
        using var compressed = new MemoryStream();
        compressed.WriteByte(0x78);
        compressed.WriteByte(0x01);
        using (var deflate = new System.IO.Compression.DeflateStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
            deflate.Write(raw, 0, raw.Length);
        Write(compressed, U32Be(Adler32(raw)));
        return compressed.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static byte[] U32Be(uint value) =>
    [
        (byte)(value >> 24),
        (byte)(value >> 16),
        (byte)(value >> 8),
        (byte)value
    ];

    private static byte[] U32Be(int value) => U32Be((uint)value);

    private static (int W, int H) PngSize(byte[] png) =>
        ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }

        return ~crc;
    }

    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string plain) => "enc:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plain));

        public string Unprotect(string protectedValue)
        {
            var payload = protectedValue["enc:".Length..];
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }
    }
}

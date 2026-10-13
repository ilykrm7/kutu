using Kutu.Core.Color;
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

        var raw = await File.ReadAllTextAsync(path);
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
    public void Pdf_wraps_jpeg_bytes()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var pdf = PdfImage.FromJpeg(jpeg, 40, 20);
        var text = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text);
        var raw = System.Text.Encoding.Latin1.GetString(pdf);
        Assert.Contains("DCTDecode", raw);
        Assert.Contains("\u00ff\u00d8\u00ff\u00d9", raw);
    }

    [Fact]
    public async Task Image_pipeline_converts_resizes_and_writes_ico()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var dir = Path.Combine(Path.GetTempPath(), "kutu-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = Path.Combine(dir, "in.png");
        await File.WriteAllBytesAsync(source, SolidPng(40, 20, 0x28, 0xA0, 0xF1));

        var jpg = Path.Combine(dir, "out.jpg");
        await ImagePipeline.ConvertAsync(source, jpg, KutuImageFormat.Jpeg, 80);
        var webp = Path.Combine(dir, "out.webp");
        await ImagePipeline.ConvertAsync(source, webp, KutuImageFormat.Webp, 70);
        var ico = Path.Combine(dir, "out.ico");
        await ImagePipeline.ConvertAsync(source, ico, KutuImageFormat.Ico, 90);
        var pdf = Path.Combine(dir, "out.pdf");
        await ImagePipeline.ConvertAsync(source, pdf, KutuImageFormat.Pdf, 80);
        var resized = Path.Combine(dir, "small.png");
        await ImagePipeline.ResizeAndSaveAsync(source, resized, FitMode.LongEdge, 10, KutuImageFormat.Png, 90);

        Assert.True(new FileInfo(jpg).Length > 0);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(jpg)[..2]);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(webp)[..4]));
        Assert.Equal(0, File.ReadAllBytes(ico)[0]);
        Assert.Equal(1, File.ReadAllBytes(ico)[2]);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(pdf)[..4]));
        var (w, h) = PngSize(await File.ReadAllBytesAsync(resized));
        Assert.Equal(10, w);
        Assert.Equal(5, h);
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

        using var compressed = new MemoryStream();
        using (var deflate = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
            deflate.Write(raw);
        var idat = compressed.ToArray();
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
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
        stream.Write(BitConverter.GetBytes(data.Length).Reverse().ToArray());
        stream.Write(typeBytes);
        stream.Write(data);
        var crcInput = typeBytes.Concat(data).ToArray();
        stream.Write(BitConverter.GetBytes(Crc32(crcInput)).Reverse().ToArray());
    }

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

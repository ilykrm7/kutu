using Kutu.Core.Imaging;
using Kutu.Core.Security;
using Kutu.Core.Text;

namespace Kutu.Core.Tests;

public class V2LogicTests
{
    [Fact]
    public void Digests_match_known_vectors()
    {
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", Digests.Compute("MD5", Digests.Utf8("")));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Digests.Compute("SHA256", Digests.Utf8("abc")));
        Assert.Equal(64, Digests.Compute("SHA256", Digests.Utf8("kutu")).Length);
        Assert.Equal(128, Digests.Compute("SHA512", Digests.Utf8("kutu")).Length);
    }

    [Fact]
    public void Uuid_v7_keeps_time_version_and_variant()
    {
        var id = UuidFormat.CreateVersion7(0x018F4E6C3C00, new byte[10]);
        var text = id.ToString();
        Assert.Equal("018f4e6c-3c00-7000-8000-000000000000", text);
        Assert.Equal('7', text[14]);
        Assert.Equal('8', text[19]);
        Assert.Equal("{018F4E6C-3C00-7000-8000-000000000000}", UuidFormat.Format(id, true, true, true));
        Assert.Equal("018f4e6c3c0070008000000000000000", UuidFormat.Format(id, false, false, false));
    }

    [Fact]
    public void Uuid_v4_uses_the_platform_guid()
    {
        var text = Guid.NewGuid().ToString();
        Assert.Equal('4', text[14]);
    }

    [Fact]
    public void Matte_clears_edge_connected_background_and_keeps_the_subject()
    {
        var pixels = Fill(5, 5, 255, 255, 255);
        Set(pixels, 5, 2, 2, 0, 0, 255);
        var cleared = Matte.ClearBackground(pixels, 5, 5, 12, holes: false, feather: 0);
        Assert.Equal(0, Alpha(cleared, 5, 0, 0));
        Assert.Equal(0, Alpha(cleared, 5, 4, 4));
        Assert.Equal(255, Alpha(cleared, 5, 2, 2));
        Assert.Equal(255, cleared[(2 * 5 + 2) * 4 + 2]);
    }

    [Fact]
    public void Matte_can_clear_enclosed_holes()
    {
        var pixels = Fill(7, 7, 255, 255, 255);
        for (var y = 2; y <= 4; y++)
        {
            for (var x = 2; x <= 4; x++)
            {
                var edge = x == 2 || x == 4 || y == 2 || y == 4;
                if (edge)
                    Set(pixels, 7, x, y, 0, 0, 0);
            }
        }

        var edgeOnly = Matte.ClearBackground(pixels, 7, 7, 8, holes: false, feather: 0);
        Assert.Equal(0, Alpha(edgeOnly, 7, 0, 0));
        Assert.Equal(255, Alpha(edgeOnly, 7, 3, 3));
        Assert.Equal(255, Alpha(edgeOnly, 7, 2, 2));

        var holes = Matte.ClearBackground(pixels, 7, 7, 8, holes: true, feather: 0);
        Assert.Equal(0, Alpha(holes, 7, 3, 3));
        Assert.Equal(255, Alpha(holes, 7, 2, 2));
    }

    [Fact]
    public void Saved_target_language_is_a_known_code()
    {
        Assert.Equal("RU", TranslateDirection.RememberedTarget("ru"));
        Assert.Equal("EN", TranslateDirection.RememberedTarget("en-US"));
        Assert.Equal("TR", TranslateDirection.RememberedTarget("nope"));
        Assert.Equal("zh-CN", TranslateDirection.GoogleCode("ZH"));
        Assert.Equal("ZH-HANS", TranslateDirection.DeepLCode("zh"));
        Assert.Equal("RU", TranslateDirection.DeepLCode("ru"));
    }

    [Fact]
    public void Screen_lines_skip_text_already_written_in_the_target_script()
    {
        Assert.True(TranslateDirection.AlreadyInTarget("Привет мир", "RU"));
        Assert.False(TranslateDirection.AlreadyInTarget("Привет мир", "EN"));
        Assert.False(TranslateDirection.AlreadyInTarget("hello world", "RU"));
        Assert.True(TranslateDirection.AlreadyInTarget("こんにちは", "JA"));
        Assert.False(TranslateDirection.AlreadyInTarget("こんにちは", "ZH"));
        var marked = ScreenLinePlan.Mark(["Привет", "merhaba"], "RU");
        Assert.True(marked[0].Skip);
        Assert.False(marked[1].Skip);
    }

    [Fact]
    public void Ocr_rank_keeps_the_first_result_until_a_clearer_one_appears()
    {
        Assert.Equal(0, ScreenLinePlan.BestOcr(["", ""]));
        Assert.Equal(1, ScreenLinePlan.BestOcr(["??", "Привет"]));
        Assert.Equal(0, ScreenLinePlan.BestOcr(["hello", "hello"]));
    }

    [Fact]
    public void Matte_feathers_the_kept_edge()
    {
        var pixels = Fill(5, 5, 255, 255, 255);
        Set(pixels, 5, 2, 2, 0, 0, 255);
        var cleared = Matte.ClearBackground(pixels, 5, 5, 12, holes: false, feather: 1);
        Assert.Equal(0, Alpha(cleared, 5, 0, 0));
        Assert.True(Alpha(cleared, 5, 2, 2) < 255);
        Assert.True(Alpha(cleared, 5, 2, 2) > 0);
    }

    private static byte[] Fill(int width, int height, byte b, byte g, byte r)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = b;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = r;
            pixels[i * 4 + 3] = 255;
        }

        return pixels;
    }

    private static void Set(byte[] pixels, int width, int x, int y, byte b, byte g, byte r)
    {
        var offset = (y * width + x) * 4;
        pixels[offset] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = 255;
    }

    private static byte Alpha(byte[] pixels, int width, int x, int y) => pixels[(y * width + x) * 4 + 3];
}

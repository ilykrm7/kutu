using System.Windows.Media.Imaging;
using Kutu.Tools.Screenshot;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Kutu.Tools.Ocr;

public sealed record OcrLineBox(string Text, double X, double Y, double Width, double Height);

public sealed class OcrPage
{
    public string Text { get; set; } = "";

    public List<OcrLineBox> Lines { get; } = [];
}

public static class WindowsOcr
{
    public static async Task<OcrPage> ReadAsync(BitmapSource source)
    {
        var engine = Engine();
        if (engine is null)
            throw new InvalidOperationException("ocr-missing");

        var png = BitmapFiles.ToPng(source);
        var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream.GetOutputStreamAt(0));
        writer.WriteBytes(png);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        stream.Seek(0);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        var bitmap = await decoder.GetSoftwareBitmapAsync();
        var result = await engine.RecognizeAsync(bitmap);
        var page = new OcrPage { Text = result.Text ?? "" };
        foreach (var line in result.Lines)
        {
            var rect = Bounds(line);
            page.Lines.Add(new OcrLineBox(line.Text ?? "", rect.X, rect.Y, rect.Width, rect.Height));
        }

        if (page.Lines.Count == 0 && page.Text.Length > 0)
            page.Lines.Add(new OcrLineBox(page.Text, 8, 8, 200, 24));
        return page;
    }

    private static OcrEngine? Engine()
    {
        var profile = OcrEngine.TryCreateFromUserProfileLanguages();
        if (profile is not null)
            return profile;

        foreach (var tag in new[] { "tr-TR", "tr", "en-US", "en" })
        {
            try
            {
                var language = new Language(tag);
                if (!OcrEngine.IsLanguageSupported(language))
                    continue;
                var engine = OcrEngine.TryCreateFromLanguage(language);
                if (engine is not null)
                    return engine;
            }
            catch
            {
                // This language tag is not a Windows language.
            }
        }

        return null;
    }

    private static Windows.Foundation.Rect Bounds(OcrLine line)
    {
        double left = double.MaxValue, top = double.MaxValue, right = 0, bottom = 0;
        var any = false;
        foreach (var word in line.Words)
        {
            any = true;
            var rect = word.BoundingRect;
            left = Math.Min(left, rect.X);
            top = Math.Min(top, rect.Y);
            right = Math.Max(right, rect.X + rect.Width);
            bottom = Math.Max(bottom, rect.Y + rect.Height);
        }

        return any
            ? new Windows.Foundation.Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top))
            : new Windows.Foundation.Rect(0, 0, 1, 1);
    }
}

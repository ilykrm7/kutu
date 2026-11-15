using System.Windows.Media.Imaging;
using Kutu.Core.Text;
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
        var engines = Engines();
        if (engines.Count == 0)
            throw new InvalidOperationException("ocr-missing");

        var png = BitmapFiles.ToPng(source);
        OcrResult? result = null;
        var best = -1;
        foreach (var engine in engines)
        {
            var stream = new InMemoryRandomAccessStream();
            var writer = new DataWriter(stream.GetOutputStreamAt(0));
            writer.WriteBytes(png);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
            stream.Seek(0);
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();
            var attempt = await engine.RecognizeAsync(bitmap);
            var score = ScreenLinePlan.OcrScore(attempt.Text);
            if (score > best)
            {
                best = score;
                result = attempt;
            }
        }

        if (result is null)
            throw new InvalidOperationException("ocr-missing");
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

    private static List<OcrEngine> Engines()
    {
        var engines = new List<OcrEngine>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(OcrEngine? engine)
        {
            if (engine is null)
                return;
            var tag = engine.RecognizerLanguage?.LanguageTag ?? "";
            if (tag.Length > 0 && !seen.Add(tag))
                return;
            engines.Add(engine);
        }

        Add(OcrEngine.TryCreateFromUserProfileLanguages());
        foreach (var language in OcrEngine.AvailableRecognizerLanguages)
            Add(OcrEngine.TryCreateFromLanguage(language));
        return engines;
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

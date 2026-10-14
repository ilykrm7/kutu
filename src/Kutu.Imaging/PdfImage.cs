using System.Text;

namespace Kutu.Imaging;

public static class PdfImage
{
    public static byte[] FromJpeg(byte[] jpeg, int width, int height)
    {
        if (jpeg is null || jpeg.Length == 0)
            throw new ArgumentException("JPEG payload is empty.", nameof(jpeg));
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        var content = $"q\n{width} 0 0 {height} 0 0 cm\n/Im Do\nQ\n";
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var objects = new string[5];
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[1] = "<< /Type /Pages /Count 1 /Kids [3 0 R] >>";
        objects[2] =
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Contents 4 0 R /Resources << /XObject << /Im 5 0 R >> >> >>";
        objects[3] = $"<< /Length {contentBytes.Length} >>";
        objects[4] =
            $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>";

        using var stream = new MemoryStream();
        void Write(string text) => WriteBytes(stream, Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n");
        var offsets = new int[6];
        for (var i = 0; i < 5; i++)
        {
            offsets[i + 1] = (int)stream.Length;
            Write($"{i + 1} 0 obj\n{objects[i]}\n");
            if (i == 3)
            {
                Write("stream\n");
                stream.Write(contentBytes, 0, contentBytes.Length);
                Write("\nendstream\n");
            }
            else if (i == 4)
            {
                Write("stream\n");
                stream.Write(jpeg, 0, jpeg.Length);
                Write("\nendstream\n");
            }

            Write("endobj\n");
        }

        var xref = (int)stream.Length;
        Write("xref\n0 6\n");
        Write("0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++)
            Write($"{offsets[i]:D10} 00000 n \n");
        Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return stream.ToArray();
    }

    public static void Write(byte[] jpeg, int width, int height, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, FromJpeg(jpeg, width, height));
    }

    private static void WriteBytes(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
}

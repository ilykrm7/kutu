using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kutu.Tools.Screenshot;

public sealed class CapturedScreen
{
    public required BitmapSource Bitmap { get; init; }

    public double ScaleX { get; init; } = 1;

    public double ScaleY { get; init; } = 1;

    public int OriginX { get; init; }

    public int OriginY { get; init; }
}

public static class ScreenCapture
{
    public static CapturedScreen Capture()
    {
        using var desktop = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        var scaleX = desktop.DpiX / 96d;
        var scaleY = desktop.DpiY / 96d;
        var dipX = SystemParameters.VirtualScreenLeft;
        var dipY = SystemParameters.VirtualScreenTop;
        var width = Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenWidth * scaleX));
        var height = Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenHeight * scaleY));
        var originX = (int)Math.Round(dipX * scaleX);
        var originY = (int)Math.Round(dipY * scaleY);
        using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(originX, originY, 0, 0, bitmap.Size, System.Drawing.CopyPixelOperation.SourceCopy);

        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return new CapturedScreen { Bitmap = source, ScaleX = scaleX, ScaleY = scaleY, OriginX = originX, OriginY = originY };
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}

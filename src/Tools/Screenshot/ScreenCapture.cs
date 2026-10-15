using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kutu.Core.Geometry;

namespace Kutu.Tools.Screenshot;

public sealed class CapturedScreen
{
    public required BitmapSource Bitmap { get; init; }

    public double ScaleX { get; init; } = 1;

    public double ScaleY { get; init; } = 1;

    public int OriginX { get; init; }

    public int OriginY { get; init; }

    public bool PerMonitor { get; init; }

    public IReadOnlyList<MonitorLayout.Band> Monitors { get; init; } = [];
}

public static class ScreenCapture
{
    public static CapturedScreen Capture()
    {
        var inputs = ReadMonitors();
        System.Drawing.Bitmap? bitmap = null;
        var originX = 0;
        var originY = 0;
        var stitched = false;
        if (inputs.Count > 0)
        {
            originX = inputs.Min(item => item.X);
            originY = inputs.Min(item => item.Y);
            var width = Math.Max(1, inputs.Max(item => item.X + item.Width) - originX);
            var height = Math.Max(1, inputs.Max(item => item.Y + item.Height) - originY);
            bitmap = Blit(width, height, graphics =>
            {
                foreach (var item in inputs)
                    graphics.CopyFromScreen(item.X, item.Y, item.X - originX, item.Y - originY, new System.Drawing.Size(item.Width, item.Height), System.Drawing.CopyPixelOperation.SourceCopy);
            });
            stitched = bitmap is not null;
        }

        if (bitmap is null || SampledBlack(bitmap))
        {
            var fallbackX = GetSystemMetrics(76);
            var fallbackY = GetSystemMetrics(77);
            var fallbackW = Math.Max(1, GetSystemMetrics(78));
            var fallbackH = Math.Max(1, GetSystemMetrics(79));
            var fallback = Blit(fallbackW, fallbackH, graphics =>
                graphics.CopyFromScreen(fallbackX, fallbackY, 0, 0, new System.Drawing.Size(fallbackW, fallbackH), System.Drawing.CopyPixelOperation.SourceCopy));
            if (fallback is not null && (bitmap is null || !SampledBlack(fallback)))
            {
                bitmap?.Dispose();
                bitmap = fallback;
                originX = fallbackX;
                originY = fallbackY;
                stitched = false;
            }
            else
            {
                fallback?.Dispose();
            }
        }

        bitmap ??= new System.Drawing.Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var source = Freeze(bitmap);
        bitmap.Dispose();
        var dipWidth = Math.Max(1d, SystemParameters.VirtualScreenWidth);
        var dipHeight = Math.Max(1d, SystemParameters.VirtualScreenHeight);
        var bands = MonitorLayout.Arrange(inputs);
        var perMonitor = stitched && MonitorLayout.Matches(bands, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, dipWidth, dipHeight);
        return new CapturedScreen
        {
            Bitmap = source,
            ScaleX = source.PixelWidth / dipWidth,
            ScaleY = source.PixelHeight / dipHeight,
            OriginX = originX,
            OriginY = originY,
            PerMonitor = perMonitor,
            Monitors = perMonitor ? bands : []
        };
    }

    private static System.Drawing.Bitmap? Blit(int width, int height, Action<System.Drawing.Graphics> copy)
    {
        if (width <= 0 || height <= 0 || width > 20000 || height > 20000)
            return null;
        var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            copy(graphics);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            return null;
        }
    }

    private static bool SampledBlack(System.Drawing.Bitmap bitmap)
    {
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            return true;
        var stepX = Math.Max(1, bitmap.Width / 24);
        var stepY = Math.Max(1, bitmap.Height / 24);
        for (var y = 0; y < bitmap.Height; y += stepY)
        {
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.R > 2 || color.G > 2 || color.B > 2)
                    return false;
            }
        }

        return true;
    }

    private static BitmapSource Freeze(System.Drawing.Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private static List<MonitorLayout.Input> ReadMonitors()
    {
        var list = new List<MonitorLayout.Input>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { cbSize = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info))
                return true;
            var width = info.rcMonitor.Right - info.rcMonitor.Left;
            var height = info.rcMonitor.Bottom - info.rcMonitor.Top;
            if (width <= 0 || height <= 0)
                return true;
            var dpiX = 96d;
            var dpiY = 96d;
            try
            {
                if (GetDpiForMonitor(monitor, 0, out var x, out var y) == 0 && x >= 48 && y >= 48)
                {
                    dpiX = x;
                    dpiY = y;
                }
            }
            catch
            {
                // shcore is absent on very old Windows. 96 keeps the stitch usable.
            }

            list.Add(new MonitorLayout.Input(info.rcMonitor.Left, info.rcMonitor.Top, width, height, dpiX, dpiY, (info.dwFlags & 1) != 0));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public RectNative rcMonitor;
        public RectNative rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}

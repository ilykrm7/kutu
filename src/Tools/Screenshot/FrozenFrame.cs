using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kutu.Tools.Screenshot;

public static class FrozenFrame
{
    public static UIElement Create(CapturedScreen screen)
    {
        if (!screen.PerMonitor || screen.Monitors.Count == 0)
            return new Image { Source = screen.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };

        var canvas = new Canvas { IsHitTestVisible = false };
        foreach (var band in screen.Monitors)
        {
            var localX = band.X - screen.OriginX;
            var localY = band.Y - screen.OriginY;
            var width = Math.Min(band.Width, screen.Bitmap.PixelWidth - localX);
            var height = Math.Min(band.Height, screen.Bitmap.PixelHeight - localY);
            if (localX < 0 || localY < 0 || width <= 0 || height <= 0)
                continue;
            var crop = new CroppedBitmap(screen.Bitmap, new Int32Rect(localX, localY, width, height));
            crop.Freeze();
            var image = new Image
            {
                Source = crop,
                Stretch = Stretch.Fill,
                Width = band.DipWidth,
                Height = band.DipHeight,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(image, band.DipX - SystemParameters.VirtualScreenLeft);
            Canvas.SetTop(image, band.DipY - SystemParameters.VirtualScreenTop);
            canvas.Children.Add(image);
        }

        return canvas.Children.Count > 0
            ? canvas
            : new Image { Source = screen.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };
    }
}

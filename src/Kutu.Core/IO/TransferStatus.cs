using System.Globalization;

namespace Kutu.Core.IO;

public sealed class TransferMeter
{
    private long _bytes;
    private long _ticks;
    private double _ema;
    private bool _started;

    public double Observe(long received, DateTime utcNow)
    {
        if (!_started)
        {
            _started = true;
            _bytes = received;
            _ticks = utcNow.Ticks;
            return 0;
        }

        var seconds = (utcNow.Ticks - _ticks) / (double)TimeSpan.TicksPerSecond;
        if (seconds < 0.2)
            return _ema;

        var instant = Math.Max(0, received - _bytes) / seconds;
        _ema = _ema <= 0 ? instant : (_ema * 0.65) + (instant * 0.35);
        _bytes = received;
        _ticks = utcNow.Ticks;
        return _ema;
    }
}

public static class TransferStatus
{
    public static string Format(long received, long total, double bytesPerSecond)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var got = received / (1024d * 1024d);
        var speed = Math.Max(0, bytesPerSecond) / (1024d * 1024d);
        if (total <= 0)
            return string.Format(culture, "{0:0.0} MB · {1:0.0} MB/sn", got, speed);

        var all = total / (1024d * 1024d);
        var remain = "kalan —";
        if (bytesPerSecond > 1 && total > received)
        {
            var secondsLeft = (int)Math.Round((total - received) / bytesPerSecond);
            if (secondsLeft < 0)
                secondsLeft = 0;
            remain = "kalan " + Clock(secondsLeft);
        }

        return string.Format(culture, "{0:0.0} / {1:0.0} MB · {2:0.0} MB/sn · {3}", got, all, speed, remain);
    }

    public static string Size(long bytes)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var mb = bytes / (1024d * 1024d);
        if (mb >= 100 || Math.Abs(mb - Math.Round(mb)) < 0.05)
            return string.Format(culture, "{0:0} MB", mb);
        return string.Format(culture, "{0:0.0} MB", mb);
    }

    private static string Clock(int seconds)
    {
        if (seconds >= 3600)
            return (seconds / 3600) + ":" + ((seconds / 60) % 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        return (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }
}

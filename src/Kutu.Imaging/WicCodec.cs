using System.Runtime.InteropServices;

namespace Kutu.Imaging;

internal static class WicCodec
{
    private static readonly Guid ClsidFactory2 = new("317d06e8-5f24-433d-bdf7-79ce68d8abc2");
    private static readonly Guid IidFactory = new("ec5ec8a9-c395-4314-9c77-54d7a935ff70");
    private static readonly Guid FormatPng = new("1b7cfaf4-713f-473c-bbcd-6137425faeaf");
    private static readonly Guid FormatJpeg = new("19e4a5aa-5662-4fc5-a0c0-1758028e1057");
    private static readonly Guid FormatBmp = new("0af1d87e-fcfe-4188-bdeb-a7906471cbe3");
    private static readonly Guid FormatWebp = new("e094b0e2-67f2-45b3-b0ea-115337ca7cf3");
    private static readonly Guid Pixel32Pbgra = new("6fddc324-4e03-4bfe-b185-3d77768dc910");

    public static void Convert(string inputPath, string outputPath, KutuImageFormat format, int quality, FitMode? fit, int fitValue)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Image conversion uses the Windows Imaging Component.");

        var factory = CreateFactory();
        object? decoder = null;
        object? frame = null;
        object? converted = null;
        object? scaled = null;
        object? encoder = null;
        object? stream = null;
        object? encoded = null;
        object? bag = null;
        try
        {
            var hr = factory.CreateDecoderFromFilename(inputPath, IntPtr.Zero, 0x80000000, 1, out decoder);
            if (hr < 0)
                throw DecodeError(hr, inputPath);

            hr = AsDecoder(decoder).GetFrame(0, out frame);
            Check(hr, "frame");
            var source = AsSource(frame);
            source.GetSize(out var width, out var height);
            hr = factory.CreateFormatConverter(out converted);
            Check(hr, "converter");
            var pixel = Pixel32Pbgra;
            hr = AsConverter(converted).Initialize(AsSource(frame), ref pixel, 0, IntPtr.Zero, 0, 0);
            Check(hr, "convert");
            var current = AsSource(converted);
            if (fit is FitMode mode)
            {
                var (tw, th) = Target(width, height, mode, fitValue);
                hr = factory.CreateBitmapScaler(out scaled);
                Check(hr, "scaler");
                hr = AsScaler(scaled).Initialize(current, tw, th, 4);
                Check(hr, "scale");
                current = AsSource(scaled);
                width = tw;
                height = th;
            }

            if (format == KutuImageFormat.Ico)
            {
                SaveIco(factory, current, outputPath);
                return;
            }

            if (format == KutuImageFormat.Pdf)
            {
                var jpegPath = Path.Combine(Path.GetTempPath(), "kutu-" + Guid.NewGuid().ToString("N") + ".jpg");
                try
                {
                Encode(factory, current, jpegPath, FormatJpeg, quality, out var jpegEncoder, out var jpegStream, out var jpegFrame, out var jpegBag);
                Release(jpegBag);
                Release(jpegFrame);
                Release(jpegEncoder);
                Release(jpegStream);
                var jpeg = File.ReadAllBytes(jpegPath);
                    PdfImage.Write(jpeg, (int)width, (int)height, outputPath);
                }
                finally
                {
                    TryDelete(jpegPath);
                }

                return;
            }

            var container = format switch
            {
                KutuImageFormat.Jpeg => FormatJpeg,
                KutuImageFormat.Webp => FormatWebp,
                KutuImageFormat.Bmp => FormatBmp,
                _ => FormatPng
            };
            Encode(factory, current, outputPath, container, quality, out encoder, out stream, out encoded, out bag);
        }
        finally
        {
            Release(bag);
            Release(encoded);
            Release(encoder);
            Release(stream);
            Release(scaled);
            Release(converted);
            Release(frame);
            Release(decoder);
            Release(factory);
        }
    }

    private static void SaveIco(IWICImagingFactory factory, IWICBitmapSource source, string outputPath)
    {
        int[] sizes = [16, 32, 48, 256];
        var pngs = new List<byte[]>(sizes.Length);
        foreach (var size in sizes)
        {
            object? scaler = null;
            var temp = Path.Combine(Path.GetTempPath(), "kutu-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Check(factory.CreateBitmapScaler(out scaler), "ico-scaler");
                Check(AsScaler(scaler).Initialize(source, (uint)size, (uint)size, 4), "ico-scale");
                Encode(factory, AsSource(scaler), temp, FormatPng, 100, out var encoder, out var stream, out var frame, out var bag);
                Release(bag);
                Release(frame);
                Release(encoder);
                Release(stream);
                pngs.Add(File.ReadAllBytes(temp));
            }
            finally
            {
                Release(scaler);
                TryDelete(temp);
            }
        }

        WriteIco(outputPath, sizes, pngs);
    }

    private static void Encode(
        IWICImagingFactory factory,
        IWICBitmapSource source,
        string outputPath,
        Guid container,
        int quality,
        out object? encoderObj,
        out object? streamObj,
        out object? frameObj,
        out object? bagObj)
    {
        encoderObj = null;
        streamObj = null;
        frameObj = null;
        bagObj = null;
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var hr = factory.CreateEncoder(ref container, IntPtr.Zero, out encoderObj);
        if (hr < 0)
            throw new InvalidOperationException(container == FormatWebp
                ? "WebP encoder is not available on this Windows install."
                : "Encoder failed: 0x" + hr.ToString("X8"));

        hr = SHCreateStreamOnFileEx(outputPath, 0x1011, 0x80, true, IntPtr.Zero, out var streamPtr);
        Check(hr, "stream");
        streamObj = Marshal.GetObjectForIUnknown(streamPtr);
        Marshal.Release(streamPtr);
        hr = AsEncoder(encoderObj).Initialize(streamObj, 2);
        Check(hr, "encoder-init");
        hr = AsEncoder(encoderObj).CreateNewFrame(out frameObj, out bagObj);
        Check(hr, "frame-encode");
        TrySetQuality(bagObj, quality);
        hr = AsFrameEncode(frameObj).Initialize(bagObj);
        Check(hr, "frame-init");
        hr = AsFrameEncode(frameObj).WriteSource(source, IntPtr.Zero);
        Check(hr, "write");
        Check(AsFrameEncode(frameObj).Commit(), "frame-commit");
        Check(AsEncoder(encoderObj).Commit(), "encoder-commit");
    }

    private static void Encode(IWICImagingFactory factory, IWICBitmapSource source, string outputPath, Guid container, int quality, out object? a, out object? b)
    {
        Encode(factory, source, outputPath, container, quality, out a, out b, out var frame, out var bag);
        Release(frame);
        Release(bag);
    }

    private static void TrySetQuality(object? bag, int quality)
    {
        if (bag is null)
            return;
        var name = Marshal.StringToCoTaskMemUni("ImageQuality");
        try
        {
            var prop = new PropBag { pstrName = name };
            var value = new PropVariant { vt = 4, flt = Math.Clamp(quality, 1, 100) / 100f };
            AsBag(bag).Write(1, ref prop, ref value);
        }
        catch
        {
            // Some containers ignore ImageQuality.
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    private static (uint W, uint H) Target(uint width, uint height, FitMode mode, int value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        double scale = mode switch
        {
            FitMode.Percent => value / 100d,
            FitMode.Width => value / (double)width,
            FitMode.Height => value / (double)height,
            _ => value / (double)Math.Max(width, height)
        };
        var w = Math.Max(1, (uint)Math.Round(width * scale));
        var h = Math.Max(1, (uint)Math.Round(height * scale));
        return (w, h);
    }

    private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using var output = File.Create(path);
        using var writer = new BinaryWriter(output);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)pngs.Count);
        var offset = 6 + (16 * pngs.Count);
        for (var i = 0; i < pngs.Count; i++)
        {
            var size = sizes[i];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(pngs[i].Length);
            writer.Write(offset);
            offset += pngs[i].Length;
        }

        foreach (var png in pngs)
            writer.Write(png);
    }

    private static Exception DecodeError(int hr, string path)
    {
        var ext = Path.GetExtension(path);
        var heif = ext.Equals(".heic", StringComparison.OrdinalIgnoreCase) || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase);
        if (heif && hr is unchecked((int)0x88982F50) or unchecked((int)0x88982F07) or unchecked((int)0x80040154))
            return new HeifCodecMissingException();
        return new InvalidOperationException("Decode failed: 0x" + hr.ToString("X8"));
    }

    private static void Check(int hr, string step)
    {
        if (hr < 0)
            throw new InvalidOperationException(step + " 0x" + hr.ToString("X8"));
    }

    private static IWICImagingFactory CreateFactory()
    {
        var hr = CoCreateInstance(ClsidFactory2, IntPtr.Zero, 1, IidFactory, out var punk);
        if (hr < 0 || punk == IntPtr.Zero)
            throw new InvalidOperationException("WIC 0x" + hr.ToString("X8"));
        return (IWICImagingFactory)Marshal.GetObjectForIUnknown(punk);
    }

    private static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.ReleaseComObject(com);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Temp files are optional.
        }
    }

    private static IWICBitmapDecoder AsDecoder(object o) => (IWICBitmapDecoder)o;
    private static IWICBitmapSource AsSource(object o) => (IWICBitmapSource)o;
    private static IWICFormatConverter AsConverter(object o) => (IWICFormatConverter)o;
    private static IWICBitmapScaler AsScaler(object o) => (IWICBitmapScaler)o;
    private static IWICBitmapEncoder AsEncoder(object o) => (IWICBitmapEncoder)o;
    private static IWICBitmapFrameEncode AsFrameEncode(object o) => (IWICBitmapFrameEncode)o;
    private static IPropertyBag2 AsBag(object o) => (IPropertyBag2)o;

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(Guid clsid, IntPtr outer, uint context, Guid iid, out IntPtr punk);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateStreamOnFileEx(string path, uint mode, uint attributes, bool create, IntPtr template, out IntPtr stream);

    [ComImport]
    [Guid("ec5ec8a9-c395-4314-9c77-54d7a935ff70")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICImagingFactory
    {
        [PreserveSig] int CreateDecoderFromFilename([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr vendor, uint access, uint options, [MarshalAs(UnmanagedType.IUnknown)] out object decoder);
        [PreserveSig] int Slot1(IntPtr a, IntPtr b, IntPtr c, uint d, [MarshalAs(UnmanagedType.IUnknown)] out object e);
        [PreserveSig] int Slot2(IntPtr a, IntPtr b, uint c, uint d, [MarshalAs(UnmanagedType.IUnknown)] out object e);
        [PreserveSig] int Slot3(ref Guid a, [MarshalAs(UnmanagedType.IUnknown)] out object b);
        [PreserveSig] int Slot4(ref Guid a, IntPtr b, [MarshalAs(UnmanagedType.IUnknown)] out object c);
        [PreserveSig] int CreateEncoder(ref Guid container, IntPtr vendor, [MarshalAs(UnmanagedType.IUnknown)] out object encoder);
        [PreserveSig] int Slot6([MarshalAs(UnmanagedType.IUnknown)] out object palette);
        [PreserveSig] int CreateFormatConverter([MarshalAs(UnmanagedType.IUnknown)] out object converter);
        [PreserveSig] int CreateBitmapScaler([MarshalAs(UnmanagedType.IUnknown)] out object scaler);
    }

    [ComImport]
    [Guid("9edde9e7-8dee-47ea-99df-e6faf2ed44bf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapDecoder
    {
        [PreserveSig] int Slot0(IntPtr a, out uint b);
        [PreserveSig] int Slot1(IntPtr a, uint b);
        [PreserveSig] int Slot2(out Guid format);
        [PreserveSig] int Slot3([MarshalAs(UnmanagedType.IUnknown)] out object info);
        [PreserveSig] int Slot4(IntPtr palette);
        [PreserveSig] int Slot5([MarshalAs(UnmanagedType.IUnknown)] out object reader);
        [PreserveSig] int Slot6([MarshalAs(UnmanagedType.IUnknown)] out object preview);
        [PreserveSig] int Slot7(uint count, IntPtr contexts, out uint actual);
        [PreserveSig] int Slot8([MarshalAs(UnmanagedType.IUnknown)] out object thumb);
        [PreserveSig] int GetFrameCount(out uint count);
        [PreserveSig] int GetFrame(uint index, [MarshalAs(UnmanagedType.IUnknown)] out object frame);
    }

    [ComImport]
    [Guid("00000120-a8f2-4877-ba0a-fd2b6645fb94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapSource
    {
        [PreserveSig] int GetSize(out uint width, out uint height);
        [PreserveSig] int GetPixelFormat(out Guid format);
        [PreserveSig] int GetResolution(out double dpiX, out double dpiY);
        [PreserveSig] int CopyPalette(IntPtr palette);
        [PreserveSig] int CopyPixels(IntPtr rect, uint stride, uint size, IntPtr buffer);
    }

    [ComImport]
    [Guid("00000301-a8f2-4877-ba0a-fd2b6645fb94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICFormatConverter : IWICBitmapSource
    {
        [PreserveSig] new int GetSize(out uint width, out uint height);
        [PreserveSig] new int GetPixelFormat(out Guid format);
        [PreserveSig] new int GetResolution(out double dpiX, out double dpiY);
        [PreserveSig] new int CopyPalette(IntPtr palette);
        [PreserveSig] new int CopyPixels(IntPtr rect, uint stride, uint size, IntPtr buffer);
        [PreserveSig] int Initialize(IWICBitmapSource source, ref Guid dstFormat, int dither, IntPtr palette, double alpha, int paletteType);
    }

    [ComImport]
    [Guid("00000302-a8f2-4877-ba0a-fd2b6645fb94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapScaler : IWICBitmapSource
    {
        [PreserveSig] new int GetSize(out uint width, out uint height);
        [PreserveSig] new int GetPixelFormat(out Guid format);
        [PreserveSig] new int GetResolution(out double dpiX, out double dpiY);
        [PreserveSig] new int CopyPalette(IntPtr palette);
        [PreserveSig] new int CopyPixels(IntPtr rect, uint stride, uint size, IntPtr buffer);
        [PreserveSig] int Initialize(IWICBitmapSource source, uint width, uint height, int mode);
    }

    [ComImport]
    [Guid("00000103-a8f2-4877-ba0a-fd2b6645fb94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapEncoder
    {
        [PreserveSig] int Initialize([MarshalAs(UnmanagedType.IUnknown)] object stream, int cache);
        [PreserveSig] int GetContainerFormat(out Guid format);
        [PreserveSig] int GetEncoderInfo([MarshalAs(UnmanagedType.IUnknown)] out object info);
        [PreserveSig] int SetColorContexts(uint count, IntPtr contexts);
        [PreserveSig] int SetPalette(IntPtr palette);
        [PreserveSig] int SetThumbnail(IntPtr thumb);
        [PreserveSig] int SetPreview(IntPtr preview);
        [PreserveSig] int CreateNewFrame([MarshalAs(UnmanagedType.IUnknown)] out object frame, [MarshalAs(UnmanagedType.IUnknown)] out object bag);
        [PreserveSig] int Commit();
        [PreserveSig] int GetMetadataQueryWriter([MarshalAs(UnmanagedType.IUnknown)] out object writer);
    }

    [ComImport]
    [Guid("00000105-a8f2-4877-ba0a-fd2b6645fb94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWICBitmapFrameEncode
    {
        [PreserveSig] int Initialize([MarshalAs(UnmanagedType.IUnknown)] object? bag);
        [PreserveSig] int SetSize(uint width, uint height);
        [PreserveSig] int SetResolution(double dpiX, double dpiY);
        [PreserveSig] int SetPixelFormat(ref Guid format);
        [PreserveSig] int SetColorContexts(uint count, IntPtr contexts);
        [PreserveSig] int SetPalette(IntPtr palette);
        [PreserveSig] int SetThumbnail(IntPtr thumb);
        [PreserveSig] int WritePixels(uint lines, uint stride, uint size, IntPtr buffer);
        [PreserveSig] int WriteSource(IWICBitmapSource source, IntPtr rect);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("22f55882-280b-11d0-a8a9-00a0c90c2004")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag2
    {
        [PreserveSig] int Read(uint count, ref PropBag bag, IntPtr err, ref PropVariant value, out uint errors);
        [PreserveSig] int Write(uint count, ref PropBag bag, ref PropVariant value);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropBag
    {
        public uint dwType;
        public ushort vt;
        public ushort cfType;
        public IntPtr dwHint;
        public IntPtr pstrName;
        public Guid clsid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public float flt;
    }
}

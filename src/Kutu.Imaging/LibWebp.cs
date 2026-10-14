using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Kutu.Core.Tests")]

namespace Kutu.Imaging;

internal static class LibWebp
{
    private static readonly object Gate = new();
    private static string? _loadedFrom;

    static LibWebp()
    {
        NativeLibrary.SetDllImportResolver(typeof(LibWebp).Assembly, Resolve);
    }

    public static void Write(byte[] bgra, int width, int height, int stride, float quality, string outputPath)
    {
        var ok = KutuWebpEncode(bgra, width, height, stride, quality, out var ptr, out var size);
        if (ok == 0 || ptr == IntPtr.Zero || size == 0)
            throw new InvalidOperationException("image");

        try
        {
            var data = new byte[size];
            Marshal.Copy(ptr, data, 0, data.Length);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(outputPath, data);
        }
        finally
        {
            KutuWebpFree(ptr);
        }
    }

    public static byte[] DecodeBgra(byte[] encoded, out int width, out int height)
    {
        var ok = KutuWebpDecode(encoded, (nuint)encoded.Length, out var ptr, out width, out height);
        if (ok == 0 || ptr == IntPtr.Zero || width <= 0 || height <= 0)
            throw new InvalidOperationException("image");

        try
        {
            var length = checked(width * height * 4);
            var pixels = new byte[length];
            Marshal.Copy(ptr, pixels, 0, length);
            return pixels;
        }
        finally
        {
            KutuWebpFree(ptr);
        }
    }

    private static IntPtr Resolve(string library, Assembly assembly, DllImportSearchPath? path)
    {
        if (!library.Equals("kutuwebp", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        return NativeLibrary.Load(Materialize());
    }

    private static string Materialize()
    {
        if (_loadedFrom is not null && File.Exists(_loadedFrom))
            return _loadedFrom;

        lock (Gate)
        {
            if (_loadedFrom is not null && File.Exists(_loadedFrom))
                return _loadedFrom;

            foreach (var candidate in Candidates())
            {
                if (File.Exists(candidate))
                    return _loadedFrom = candidate;
            }

            using var stream = typeof(LibWebp).Assembly.GetManifestResourceStream("kutuwebp.dll")
                ?? throw new InvalidOperationException("image");
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kutu", "kutuwebp.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            if (!File.Exists(cache) || new FileInfo(cache).Length != stream.Length)
            {
                using var file = File.Create(cache);
                stream.CopyTo(file);
            }

            return _loadedFrom = cache;
        }
    }

    private static IEnumerable<string> Candidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "kutuwebp.dll");
        var process = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(process))
            yield return Path.Combine(Path.GetDirectoryName(process) ?? "", "kutuwebp.dll");
    }

    [DllImport("kutuwebp", CallingConvention = CallingConvention.Cdecl)]
    private static extern int KutuWebpEncode(byte[] bgra, int width, int height, int stride, float quality, out IntPtr output, out nuint size);

    [DllImport("kutuwebp", CallingConvention = CallingConvention.Cdecl)]
    private static extern int KutuWebpDecode(byte[] data, nuint size, out IntPtr bgra, out int width, out int height);

    [DllImport("kutuwebp", CallingConvention = CallingConvention.Cdecl)]
    private static extern void KutuWebpFree(IntPtr ptr);
}

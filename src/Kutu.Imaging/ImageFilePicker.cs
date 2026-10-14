using System.Runtime.InteropServices;
using Kutu.Core;

namespace Kutu.Imaging;

public static class ImageFilePicker
{
    private const int Explorer = 0x00080000;
    private const int AllowMultiSelect = 0x00000200;
    private const int FileMustExist = 0x00001000;
    private const int PathMustExist = 0x00000800;
    private const int HideReadOnly = 0x00000004;

    public static string[]? Pick(IntPtr owner)
    {
        if (!HostProcess.IsWindows)
            return null;

        const int chars = 65536;
        var fileBuffer = Marshal.AllocHGlobal(chars * 2);
        var filter = "Images\0*.png;*.jpg;*.jpeg;*.jfif;*.webp;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.heic;*.heif\0\0";
        var filterChars = filter.ToCharArray();
        var filterBuffer = Marshal.AllocHGlobal(filterChars.Length * 2);
        try
        {
            for (var i = 0; i < chars; i++)
                Marshal.WriteInt16(fileBuffer, i * 2, 0);
            Marshal.Copy(filterChars, 0, filterBuffer, filterChars.Length);
            var dialog = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                hwndOwner = owner,
                lpstrFilter = filterBuffer,
                lpstrFile = fileBuffer,
                nMaxFile = chars,
                Flags = Explorer | AllowMultiSelect | FileMustExist | PathMustExist | HideReadOnly,
                nFilterIndex = 1
            };
            if (!GetOpenFileName(ref dialog))
                return null;

            return ImageFileList.CombineSelection(ReadSegments(fileBuffer, chars)).ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filterBuffer);
        }
    }

    private static List<string> ReadSegments(IntPtr buffer, int maxChars)
    {
        var segments = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < maxChars; i++)
        {
            var ch = (char)Marshal.ReadInt16(buffer, i * 2);
            if (ch != '\0')
            {
                current.Append(ch);
                continue;
            }

            if (current.Length == 0)
                break;
            segments.Add(current.ToString());
            current.Clear();
        }

        return segments;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetOpenFileNameW")]
    private static extern bool GetOpenFileName(ref OpenFileName dialog);
}

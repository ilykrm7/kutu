using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Kutu.Core.Text;

public static class SelectionGrab
{
    private const int WmCopy = 0x0301;
    private const uint CfUnicodeText = 13;

    public static string LastDiagnostic { get; private set; } = "";

    public static string Capture()
    {
        var released = WaitUntilReleased(TimeSpan.FromMilliseconds(500));
        var cleared = TryClear(out var sequence);
        var target = FocusedControl(out var className);
        if (target != IntPtr.Zero)
            SendMessage(target, WmCopy, IntPtr.Zero, IntPtr.Zero);

        var text = WaitForText(sequence, 150);
        var usedChord = false;
        if (text is null)
        {
            usedChord = true;
            InputPump.Send(KeyStrokePlan.Chord(0x11, 0x43));
            text = WaitForText(sequence, 800);
        }

        LastDiagnostic = $"released={released} cleared={cleared} hwnd=0x{target.ToInt64():X} class={className} chord={usedChord} seq={sequence}->{GetClipboardSequenceNumber()}";
        return text ?? "";
    }

    public static string Translate(Func<string, string> translate)
    {
        var selected = Capture();
        if (string.IsNullOrWhiteSpace(selected))
            return "";

        var next = translate(selected) ?? "";
        InputPump.Send(KeyStrokePlan.Unicode(next));
        return next;
    }

    private static bool WaitUntilReleased(TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (!Held(0x11) && !Held(0xA2) && !Held(0xA3) && !Held(0x12) && !Held(0xA4) && !Held(0xA5) && !Held(0x54))
                return true;
            Thread.Sleep(15);
        }

        return !Held(0x11) && !Held(0xA2) && !Held(0xA3) && !Held(0x12) && !Held(0xA4) && !Held(0xA5) && !Held(0x54);
    }

    private static bool Held(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static IntPtr FocusedControl(out string className)
    {
        className = "";
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return IntPtr.Zero;

        var thread = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        var focus = IntPtr.Zero;
        if (GetGUIThreadInfo(thread, ref info))
            focus = info.Focus != IntPtr.Zero ? info.Focus : info.Active;
        if (focus == IntPtr.Zero)
            focus = foreground;
        if (!IsEdit(focus))
        {
            var nested = FindEdit(focus);
            if (nested == IntPtr.Zero && focus != foreground)
                nested = FindEdit(foreground);
            if (nested != IntPtr.Zero)
                focus = nested;
        }

        className = ClassName(focus);
        return focus;
    }

    private static bool IsEdit(IntPtr hwnd)
    {
        var name = ClassName(hwnd);
        return name.Equals("Edit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase);
    }

    private static IntPtr FindEdit(IntPtr parent)
    {
        var found = IntPtr.Zero;
        EnumChildWindows(parent, (hwnd, _) =>
        {
            if (!IsEdit(hwnd))
                return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static string ClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private static bool TryClear(out uint sequence)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                Thread.Sleep(30);
                continue;
            }

            try
            {
                EmptyClipboard();
            }
            finally
            {
                CloseClipboard();
            }

            sequence = GetClipboardSequenceNumber();
            return true;
        }

        sequence = GetClipboardSequenceNumber();
        return false;
    }

    private static string? WaitForText(uint baseline, int timeoutMs)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds <= timeoutMs)
        {
            if (GetClipboardSequenceNumber() != baseline)
            {
                if (TryReadUnicode(out var text))
                    return text;
            }

            if (clock.ElapsedMilliseconds >= timeoutMs)
                break;
            Thread.Sleep(20);
        }

        return null;
    }

    private static bool TryReadUnicode(out string text)
    {
        text = "";
        if (!OpenClipboard(IntPtr.Zero))
            return false;

        try
        {
            var data = GetClipboardData(CfUnicodeText);
            if (data == IntPtr.Zero)
                return false;

            var locked = GlobalLock(data);
            if (locked == IntPtr.Zero)
                return false;
            try
            {
                text = Marshal.PtrToStringUni(locked) ?? "";
                return true;
            }
            finally
            {
                GlobalUnlock(data);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public int Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public int CaretLeft;
        public int CaretTop;
        public int CaretRight;
        public int CaretBottom;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);
}

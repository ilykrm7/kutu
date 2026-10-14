using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Kutu.Core.Text;

namespace Kutu.Core.Tests;

public class NotepadSelectionTests
{
    [Fact]
    public void Notepad_selection_translates_to_uppercase()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        Process? notepad = null;
        try
        {
            notepad = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = false });
            Assert.NotNull(notepad);
            var (frame, edit) = WaitForEdit(notepad!, TimeSpan.FromSeconds(12));
            Drive(frame, edit, "kutu");
            var body = ReadText(edit).Trim();
            Assert.True(body == "KUTU", $"window='{body}' {SelectionGrab.LastDiagnostic}");
        }
        finally
        {
            try
            {
                if (notepad is { HasExited: false })
                {
                    notepad.Kill();
                    notepad.WaitForExit(2000);
                }
            }
            catch
            {
                // The window may already be gone.
            }

            notepad?.Dispose();
        }
    }

    private static void Drive(IntPtr frame, IntPtr edit, string word)
    {
        Focus(frame, edit);
        var typed = InputPump.Send(KeyStrokePlan.Unicode(word));
        Assert.True(typed.Ok, "typing failed " + typed.Win32Error);
        Thread.Sleep(250);
        Focus(frame, edit);
        var selected = InputPump.Send(KeyStrokePlan.Chord(0x11, 0x41));
        Assert.True(selected.Ok, "select all failed " + selected.Win32Error);
        Thread.Sleep(200);
        Focus(frame, edit);
        var replaced = SelectionGrab.Translate(text => text.ToUpperInvariant());
        if (!string.Equals(replaced, word.ToUpperInvariant(), StringComparison.Ordinal))
        {
            Thread.Sleep(200);
            Focus(frame, edit);
            replaced = SelectionGrab.Translate(text => text.ToUpperInvariant());
        }

        Assert.True(
            string.Equals(replaced.Trim(), word.ToUpperInvariant(), StringComparison.Ordinal),
            $"replaced='{replaced}' {SelectionGrab.LastDiagnostic}");
        Thread.Sleep(300);
    }

    private static (IntPtr Frame, IntPtr Edit) WaitForEdit(Process process, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            process.Refresh();
            var frame = process.MainWindowHandle;
            if (frame != IntPtr.Zero)
            {
                var edit = FindEdit(frame);
                if (edit != IntPtr.Zero)
                    return (frame, edit);
            }

            Thread.Sleep(100);
        }

        throw new InvalidOperationException("Notepad edit control did not appear");
    }

    private static IntPtr FindEdit(IntPtr parent)
    {
        var found = IntPtr.Zero;
        EnumChildWindows(parent, (hwnd, _) =>
        {
            var name = ClassName(hwnd);
            if (!name.Equals("Edit", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase))
                return true;
            found = hwnd;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static void Focus(IntPtr frame, IntPtr edit)
    {
        var current = GetCurrentThreadId();
        var foreground = GetForegroundWindow();
        var foreThread = GetWindowThreadProcessId(foreground, out _);
        var editThread = GetWindowThreadProcessId(edit, out _);
        AllowSetForegroundWindow(0xFFFFFFFF);
        AttachThreadInput(current, foreThread, true);
        AttachThreadInput(current, editThread, true);
        ShowWindow(frame, 9);
        SetWindowPos(frame, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
        SetForegroundWindow(frame);
        BringWindowToTop(frame);
        SetFocus(edit);
        SetWindowPos(frame, new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
        AttachThreadInput(current, editThread, false);
        AttachThreadInput(current, foreThread, false);
        Thread.Sleep(80);
    }

    private static string ReadText(IntPtr edit)
    {
        var buffer = new StringBuilder(2048);
        SendMessage(edit, 0x000D, (IntPtr)buffer.Capacity, buffer);
        return buffer.ToString();
    }

    private static string ClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private delegate bool EnumProc(IntPtr hwnd, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int capacity);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint source, uint target, bool attach);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, StringBuilder text);
}

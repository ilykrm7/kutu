using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Kutu.Core.Models;
using Kutu.Core.Settings;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Windows;

namespace Kutu.Tools.TextExpander;

public sealed class TextExpanderService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmQuit = 0x0012;
    private const int WmTimer = 0x0113;
    private const int WhMouseLl = 14;
    private readonly IKutuHost _host;
    private readonly ExpanderBuffer _buffer = new();
    private readonly ExpanderCommit _commit = new();
    private readonly LowLevelKeyboardProc _proc;
    private readonly LowLevelKeyboardProc _mouseProc;
    private IntPtr _hook;
    private IntPtr _mouseHook;
    private IntPtr _foreground;
    private Thread? _thread;
    private uint _threadId;
    private bool _shift;
    private bool _ctrl;
    private bool _alt;
    private bool _win;

    public TextExpanderService(IKutuHost host)
    {
        _host = host;
        _proc = Hook;
        _mouseProc = MouseHook;
    }

    public void Start()
    {
        if (_thread is not null)
            return;

        KeyInjector.Start();
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "Kutu.Expander"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Dispose()
    {
        if (_threadId != 0)
            PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
    }

    private void Loop()
    {
        OleInitialize(IntPtr.Zero);
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        _foreground = GetForegroundWindow();
        var module = HookModule();
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, module, 0);
        if (_hook == IntPtr.Zero)
            DebugLog("hook failed error=" + Marshal.GetLastWin32Error() + " module=" + module, force: true);
        else if (_host.Settings.ExpanderDebug)
            DebugLog("hook installed module=" + module);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, module, 0);
        SetTimer(IntPtr.Zero, (IntPtr)1, 1500, IntPtr.Zero);
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.MsgId == WmTimer)
                NoteElevation();
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        KillTimer(IntPtr.Zero, (IntPtr)1);
        if (_hook != IntPtr.Zero)
            UnhookWindowsHookEx(_hook);
        if (_mouseHook != IntPtr.Zero)
            UnhookWindowsHookEx(_mouseHook);
        OleUninitialize();
    }

    private IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || (wParam != (IntPtr)WmKeyDown && wParam != (IntPtr)WmKeyUp && wParam != (IntPtr)WmSysKeyDown && wParam != (IntPtr)WmSysKeyUp))
            return CallNextHookEx(_hook, nCode, wParam, lParam);

        KeyInjector.EnterHook();
        try
        {
            var data = Marshal.PtrToStructure<KbdLlHook>(lParam);
            if (ExpanderHook.ShouldHandle(data.Flags, KeyInjector.IsBusy, data.Extra))
            {
                var down = wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown;
                TrackModifier(data.VkCode, down);
                NoteFocus();
                var swallow = false;
                if (down && wParam == (IntPtr)WmKeyDown && _host.Settings.TextExpanderEnabled && !IsOwnProcess())
                    swallow = HandleKey(data.VkCode, data.ScanCode, down: true);
                else if (!down && wParam == (IntPtr)WmKeyUp && _host.Settings.TextExpanderEnabled && !IsOwnProcess())
                    swallow = HandleKey(data.VkCode, data.ScanCode, down: false);
                if (swallow)
                    return (IntPtr)1;
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
        finally
        {
            KeyInjector.LeaveHook();
        }
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)0x0201 || wParam == (IntPtr)0x0204 || wParam == (IntPtr)0x0207 || wParam == (IntPtr)0x020B))
        {
            _commit.Cancel();
            _buffer.MouseClick();
            NoteFocus();
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void NoteFocus()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == _foreground)
            return;
        _foreground = hwnd;
        _commit.Cancel();
        _buffer.FocusChanged();
        ReleaseModifiers();
    }

    private void ReleaseModifiers()
    {
        _ctrl = false;
        _alt = false;
        _win = false;
    }

    private void TrackModifier(uint vk, bool down)
    {
        if (vk is 0x10 or 0xA0 or 0xA1)
            _shift = down;
        else if (vk is 0x11 or 0xA2 or 0xA3)
            _ctrl = down;
        else if (vk is 0x12 or 0xA4 or 0xA5)
            _alt = down;
        else if (vk is 0x5B or 0x5C)
            _win = down;
    }

    private bool HandleKey(uint vk, uint scan, bool down)
    {
        if (!down)
        {
            if (_commit.KeyUp(vk, out var snippet, out var delete) && snippet is not null)
                Schedule(snippet, delete);
            return false;
        }

        if (_commit.HasPending)
        {
            _commit.KeyDown(vk, null);
            if (_commit.SwallowRepeat)
                return true;
            if (_commit.Flush(out var early, out var earlyDelete) && early is not null)
                Schedule(early, earlyDelete);
        }

        if (vk == 0x08)
        {
            _commit.Cancel();
            _buffer.Backspace();
            return false;
        }

        if (vk is 0x10 or 0xA0 or 0xA1 or 0x11 or 0xA2 or 0xA3 or 0x12 or 0xA4 or 0xA5 or 0x5B or 0x5C)
            return false;

        if (vk is 0x0D)
        {
            _commit.Cancel();
            _buffer.Enter();
            ReleaseModifiers();
            return false;
        }

        if (vk is 0x09)
        {
            _commit.Cancel();
            _buffer.Tab();
            ReleaseModifiers();
            return false;
        }

        if (vk is 0x1B)
        {
            _commit.Cancel();
            _buffer.Escape();
            ReleaseModifiers();
            return false;
        }

        if (vk is 0x25 or 0x26 or 0x27 or 0x28 or 0x24 or 0x23)
        {
            _commit.Cancel();
            _buffer.Clear();
            return false;
        }

        var text = ToChar(vk, scan);
        if (string.IsNullOrEmpty(text))
            text = FallbackChar(vk);
        var match = _buffer.Type(text, _host.Settings.Snippets, _win, _ctrl, _alt);
        LogTrigger(text, match?.Trigger);
        _commit.KeyDown(vk, match);
        if (match is not null)
            _buffer.Clear();
        return false;
    }

    private void Schedule(Snippet snippet, int delete)
    {
        if (string.IsNullOrEmpty(snippet.Body) || delete <= 0)
            return;

        var body = snippet.Body;
        _buffer.Clear();
        DebugLog($"scheduled delete={delete}");
        KeyInjector.Run(() => ReplaceOnWorker(body, delete));
    }

    private void LogTrigger(string? text, string? trigger)
    {
        if (!_host.Settings.ExpanderDebug)
            return;
        var visible = ExpanderLogText.VisibleBuffer(_buffer.Text);
        if (visible is null && string.IsNullOrEmpty(trigger))
            return;
        DebugLog($"ch={(visible is null ? "*" : text)} buf={visible ?? "*"} match={trigger}");
    }

    private void ReplaceOnWorker(string body, int delete)
    {
        try
        {
            if (ForegroundElevation.BlocksTyping())
            {
                DebugLog("inject skipped elevated");
                ForegroundElevation.NotifyOnce(_host);
                return;
            }

            var released = KeyInjector.Send(KeyStrokePlan.ReleaseModifiers());
            if (!released.Ok)
                DebugLog($"modifiers result={released.AcceptedEvents} err={released.Win32Error}");

            var deleted = KeyInjector.Send(KeyStrokePlan.Backspaces(delete));
            DebugLog($"backspaces sent={deleted.KeyCount} result={deleted.AcceptedEvents} err={deleted.Win32Error}");
            Thread.Sleep(30);
            var typed = KeyInjector.Send(KeyStrokePlan.Unicode(body));
            DebugLog($"unicode sent={typed.AcceptedEvents} result={typed.AcceptedEvents} err={typed.Win32Error}");
            if (typed.Ok)
                return;

            _host.SuppressClipboard(TimeSpan.FromMilliseconds(900));
            var backup = KeyInjector.Snapshot();
            try
            {
                var pasted = KeyInjector.PasteText(body);
                DebugLog($"paste result={pasted.AcceptedEvents} err={pasted.Win32Error}");
                Thread.Sleep(80);
            }
            finally
            {
                KeyInjector.Restore(backup);
            }
        }
        catch (Exception ex)
        {
            DebugLog("inject failed " + ex.GetType().Name + " " + ex.Message, force: true);
        }
    }

    private string? ToChar(uint vk, uint scan)
    {
        var state = new byte[256];
        var shift = _shift || (GetAsyncKeyState(0x10) & 0x8000) != 0 || (GetAsyncKeyState(0xA0) & 0x8000) != 0 || (GetAsyncKeyState(0xA1) & 0x8000) != 0;
        if (shift)
        {
            state[0x10] = 0x80;
            state[0xA0] = 0x80;
        }

        if ((GetKeyState(0x14) & 1) != 0)
            state[0x14] = 0x01;
        if (vk < 256)
            state[vk] = 0x80;
        var foreground = GetForegroundWindow();
        var thread = GetWindowThreadProcessId(foreground, out _);
        var layout = GetKeyboardLayout(thread);
        var buffer = new StringBuilder(8);
        var written = ToUnicodeEx(vk, scan, state, buffer, buffer.Capacity, 0, layout);
        if (written < 0)
            ToUnicodeEx(vk, scan, state, buffer, buffer.Capacity, 0, layout);
        return written > 0 ? buffer.ToString() : null;
    }

    private string? FallbackChar(uint vk)
    {
        if (vk is >= 0x41 and <= 0x5A)
        {
            var letter = (char)('a' + (vk - 0x41));
            var caps = (GetKeyState(0x14) & 1) != 0;
            if (_shift ^ caps)
                letter = char.ToUpperInvariant(letter);
            return letter.ToString();
        }

        if (vk is >= 0x30 and <= 0x39 && !_shift)
            return ((char)vk).ToString();
        if (vk == 0xBE)
            return _shift ? ":" : ".";
        return null;
    }

    private void DebugLog(string line, bool force = false)
    {
        if (!force && !_host.Settings.ExpanderDebug)
            return;

        try
        {
            var dir = SettingsService.AppDirectory;
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "expander.log"), $"{DateTimeOffset.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
        catch
        {
            // Debug logging must not stop the hook.
        }
    }

    private static IntPtr HookModule()
    {
        try
        {
            var name = Process.GetCurrentProcess().MainModule?.ModuleName;
            if (!string.IsNullOrWhiteSpace(name))
            {
                var handle = GetModuleHandle(name);
                if (handle != IntPtr.Zero)
                    return handle;
            }
        }
        catch
        {
            // Single-file hosts can hide the module name.
        }

        return GetModuleHandle(null);
    }

    private void NoteElevation()
    {
        try
        {
            if (_host.Settings.TextExpanderEnabled && ForegroundElevation.BlocksTyping())
                ForegroundElevation.NotifyOnce(_host);
        }
        catch
        {
            // Elevation checks must not stop the hook thread.
        }
    }

    private static bool IsOwnProcess()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Process.GetCurrentProcess().Id;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHook
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint MsgId;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out Msg message, IntPtr hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg message, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint id, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint milliseconds, IntPtr callback);

    [DllImport("user32.dll")]
    private static extern bool KillTimer(IntPtr hwnd, IntPtr id);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();
}

using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Windows;

namespace Kutu.Tools.TextExpander;

public sealed class TextExpanderService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmQuit = 0x0012;
    private const int WmTimer = 0x0113;
    private const int WmApp = 0x8000;
    private readonly IKutuHost _host;
    private readonly StringBuilder _buffer = new();
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook;
    private Thread? _thread;
    private uint _threadId;
    private int _injecting;
    private string? _pendingBody;
    private int _pendingLength;

    public TextExpanderService(IKutuHost host)
    {
        _host = host;
        _proc = Hook;
    }

    public void Start()
    {
        if (_thread is not null)
            return;

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
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        SetTimer(IntPtr.Zero, (IntPtr)1, 1500, IntPtr.Zero);
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.MsgId == WmApp)
                ReplacePending();
            else if (message.MsgId == WmTimer)
                NoteElevation();
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        KillTimer(IntPtr.Zero, (IntPtr)1);
        if (_hook != IntPtr.Zero)
            UnhookWindowsHookEx(_hook);
        OleUninitialize();
    }

    private IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WmKeyDown && Volatile.Read(ref _injecting) == 0 && _host.Settings.TextExpanderEnabled)
        {
            var data = Marshal.PtrToStructure<KbdLlHook>(lParam);
            if ((data.Flags & 0x12) == 0 && !IsOwnProcess())
                HandleKey(data.VkCode, data.ScanCode);
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void HandleKey(uint vk, uint scan)
    {
        if (vk == 0x08)
        {
            if (_buffer.Length > 0)
                _buffer.Length -= 1;
            return;
        }

        if ((GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0 || (GetAsyncKeyState(0x5B) & 0x8000) != 0)
        {
            if (vk is not (0x10 or 0xA0 or 0xA1))
                _buffer.Clear();
            return;
        }

        if (vk is 0x25 or 0x26 or 0x27 or 0x28 or 0x1B or 0x24 or 0x23 or 0x09)
        {
            _buffer.Clear();
            return;
        }

        var text = ToChar(vk, scan);
        if (string.IsNullOrEmpty(text))
            return;

        _buffer.Append(text);
        if (_buffer.Length > 128)
            _buffer.Remove(0, _buffer.Length - 128);

        var match = SnippetMatcher.Match(_buffer.ToString(), _host.Settings.Snippets);
        if (match is null || string.IsNullOrEmpty(match.Body) || string.IsNullOrEmpty(match.Trigger))
            return;

        _pendingLength = match.Trigger.Trim().Length;
        _pendingBody = match.Body;
        _buffer.Clear();
        PostThreadMessage(_threadId, WmApp, IntPtr.Zero, IntPtr.Zero);
    }

    private void ReplacePending()
    {
        var body = _pendingBody;
        var length = _pendingLength;
        _pendingBody = null;
        if (string.IsNullOrEmpty(body) || length <= 0)
            return;

        if (ForegroundElevation.BlocksTyping())
        {
            ForegroundElevation.NotifyOnce(_host);
            return;
        }

        Interlocked.Exchange(ref _injecting, 1);
        try
        {
            _host.SuppressClipboard(TimeSpan.FromMilliseconds(900));
            var backup = Snapshot();
            var deleted = SendBackspaces(length);
            Thread.Sleep(40);
            Clipboard.SetText(body);
            var pasted = SendCtrlV();
            Thread.Sleep(80);
            Restore(backup);
            if ((!deleted || !pasted) && ForegroundElevation.BlocksTyping())
                ForegroundElevation.NotifyOnce(_host);
        }
        catch
        {
            // Leave the typed trigger in place if injection fails.
        }
        finally
        {
            Interlocked.Exchange(ref _injecting, 0);
        }
    }

    private static string? ToChar(uint vk, uint scan)
    {
        var state = new byte[256];
        void Mark(int key)
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
                state[key] = 0x80;
        }

        Mark(0x10);
        Mark(0xA0);
        Mark(0xA1);
        Mark(0x14);
        if ((GetKeyState(0x14) & 1) != 0)
            state[0x14] = 0x01;
        var foreground = GetForegroundWindow();
        var thread = GetWindowThreadProcessId(foreground, out _);
        var layout = GetKeyboardLayout(thread);
        var buffer = new StringBuilder(8);
        var written = ToUnicodeEx(vk, scan, state, buffer, buffer.Capacity, 4, layout);
        return written > 0 ? buffer.ToString() : null;
    }

    private static IDataObject? Snapshot()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null)
                return null;
            var copy = new DataObject();
            foreach (var format in data.GetFormats() ?? [])
            {
                try
                {
                    var value = data.GetData(format);
                    if (value is not null)
                        copy.SetData(format, value);
                }
                catch
                {
                    // Some formats cannot be copied.
                }
            }

            return copy;
        }
        catch
        {
            return null;
        }
    }

    private static void Restore(IDataObject? data)
    {
        try
        {
            if (data is null)
                Clipboard.Clear();
            else
                Clipboard.SetDataObject(data, true);
        }
        catch
        {
            // The previous clipboard contents could not be restored.
        }
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

    private static bool SendBackspaces(int count)
    {
        var inputs = new Input[count * 2];
        for (var i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyInput(0x08, false);
            inputs[(i * 2) + 1] = KeyInput(0x08, true);
        }

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == (uint)inputs.Length;
    }

    private static bool SendCtrlV()
    {
        Input[] inputs = [KeyInput(0x11, false), KeyInput(0x56, false), KeyInput(0x56, true), KeyInput(0x11, true)];
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == (uint)inputs.Length;
    }

    private static Input KeyInput(ushort vk, bool up) => new()
    {
        Type = 1,
        Union = new InputUnion { Keyboard = new KeyboardInput { Vk = vk, Flags = up ? 0x0002u : 0 } }
    };

    private static bool IsOwnProcess()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr Extra;
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
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);

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

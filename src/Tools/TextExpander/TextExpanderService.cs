using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Kutu.Core.Text;
using Kutu.Sdk.Hosting;

namespace Kutu.Tools.TextExpander;

public sealed class TextExpanderService : IDisposable
{
    private readonly IKutuHost _host;
    private readonly StringBuilder _buffer = new();
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook;
    private Dispatcher? _dispatcher;
    private int _injecting;

    public TextExpanderService(IKutuHost host)
    {
        _host = host;
        _proc = Hook;
    }

    public void Start()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _hook = SetWindowsHookEx(13, _proc, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == 0x0100 || wParam == 0x0104) && Volatile.Read(ref _injecting) == 0 && !IsOwnProcess())
        {
            var data = Marshal.PtrToStructure<KbdLlHook>(lParam);
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

        if (vk is 0x25 or 0x26 or 0x27 or 0x28 or 0x1B or 0x24 or 0x23 or 0x0D or 0x09)
        {
            _buffer.Clear();
            return;
        }

        var text = ToChar(vk, scan);
        if (string.IsNullOrEmpty(text))
            return;

        _buffer.Append(text);
        if (_buffer.Length > 96)
            _buffer.Remove(0, _buffer.Length - 96);

        var match = SnippetMatcher.Match(_buffer.ToString(), _host.Settings.Snippets);
        if (match is null || string.IsNullOrEmpty(match.Body))
            return;

        var triggerLength = match.Trigger.Trim().Length;
        var body = match.Body;
        _buffer.Clear();
        _dispatcher?.BeginInvoke(new Action(() => { _ = ReplaceAsync(triggerLength, body); }));
    }

    private async Task ReplaceAsync(int triggerLength, string body)
    {
        Interlocked.Exchange(ref _injecting, 1);
        try
        {
            _host.SuppressClipboard(TimeSpan.FromMilliseconds(900));
            var backup = Snapshot();
            SendBackspaces(triggerLength);
            await Task.Delay(50);
            Clipboard.SetText(body);
            SendCtrlV();
            await Task.Delay(120);
            Restore(backup);
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

    private static void SendBackspaces(int count)
    {
        if (count <= 0)
            return;

        var inputs = new Input[count * 2];
        for (var i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyInput(0x08, false);
            inputs[(i * 2) + 1] = KeyInput(0x08, true);
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(0x11, false),
            KeyInput(0x56, false),
            KeyInput(0x56, true),
            KeyInput(0x11, true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(ushort vk, bool up) => new()
    {
        Type = 1,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                Vk = vk,
                Flags = up ? 0x0002u : 0
            }
        }
    };

    private static string? ToChar(uint vk, uint scan)
    {
        var state = new byte[256];
        if (!GetKeyboardState(state))
            return null;

        var buffer = new StringBuilder(8);
        var written = ToUnicodeEx(vk, scan, state, buffer, buffer.Capacity, 0, GetKeyboardLayout(0));
        return written > 0 ? buffer.ToString() : null;
    }

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
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr Extra;
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
    private static extern bool GetKeyboardState(byte[] lpKeyState);

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
}

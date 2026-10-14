using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Kutu.Core.Diagnostics;
using Kutu.Core.Hotkeys;

namespace Kutu.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 1;
    private IntPtr _hwnd;
    private HwndSource? _source;

    public void Attach(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public void Clear()
    {
        foreach (var id in _actions.Keys.ToList())
            UnregisterHotKey(_hwnd, id);
        _actions.Clear();
    }

    public bool Register(string gestureText, Action action)
    {
        if (!HotkeyGesture.TryParse(gestureText, out var gesture) || !TryKey(gesture.Key, out var key))
        {
            AppLog.Write("hotkey not parsed: " + gestureText);
            return false;
        }

        if (_hwnd == IntPtr.Zero)
        {
            AppLog.Write("hotkey not registered, window handle is zero: " + gestureText);
            return false;
        }

        uint modifiers = 0x4000;
        if (gesture.Alt) modifiers |= 0x0001;
        if (gesture.Control) modifiers |= 0x0002;
        if (gesture.Shift) modifiers |= 0x0004;
        if (gesture.Win) modifiers |= 0x0008;
        var id = _nextId++;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!TryRegister(id, modifiers, virtualKey))
        {
            var error = Marshal.GetLastWin32Error();
            if (!TryRegister(id, modifiers & ~0x4000u, virtualKey))
            {
                AppLog.Write($"hotkey failed {gestureText} vk={virtualKey} error={error}");
                return false;
            }
        }

        _actions[id] = action;
        return true;
    }

    public void Dispose() => Clear();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
                Run(action);
            else
                dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => Run(action)));
        }

        return IntPtr.Zero;
    }

    private static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
        }
    }

    private bool TryRegister(int id, uint modifiers, uint virtualKey) =>
        RegisterHotKey(_hwnd, id, modifiers, virtualKey);

    private static bool TryKey(string name, out Key key)
    {
        if (name.Equals("PrintScreen", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Snapshot;
            return true;
        }

        return Enum.TryParse(name, ignoreCase: true, out key);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

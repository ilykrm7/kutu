using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kutu.Sdk.Hosting;
using Kutu.Sdk.Localization;
using Kutu.Sdk.Windows;

namespace Kutu.Tools.Translate;

public static class SelectionTranslator
{
    public static async Task RunAsync(IKutuHost host)
    {
        if (ForegroundElevation.BlocksTyping())
        {
            ForegroundElevation.NotifyOnce(host);
            return;
        }

        var toast = TinyToast.Show(L.Get("Translate_Working"));
        IDataObject? backup = null;
        try
        {
            host.SuppressClipboard(TimeSpan.FromMilliseconds(1200));
            backup = Snapshot();
            try
            {
                Clipboard.Clear();
            }
            catch
            {
                // The clipboard can be busy for a moment.
            }

            SendCtrl(0x43);
            var text = await WaitForTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                Restore(backup);
                toast.Set(L.Get("Translate_NoSelection"));
                await Task.Delay(900);
                return;
            }

            var result = await TranslateClient.TranslateAsync(text, "auto", "EN", host.Settings.DeepLKey, host.Settings.DeepLEndpoint);
            Clipboard.SetText(string.IsNullOrWhiteSpace(result.Text) ? text : result.Text);
            SendCtrl(0x56);
            await Task.Delay(120);
            Restore(backup);
        }
        catch (Exception ex)
        {
            Restore(backup);
            toast.Set(ex.Message.Length > 80 ? ex.Message[..80] : ex.Message);
            await Task.Delay(1200);
        }
        finally
        {
            toast.Close();
        }
    }

    private static async Task<string> WaitForTextAsync()
    {
        for (var i = 0; i < 8; i++)
        {
            await Task.Delay(40);
            try
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
            catch
            {
                // Retry while the clipboard is updating.
            }
        }

        return "";
    }

    private static IDataObject? Snapshot()
    {
        try
        {
            return Clipboard.GetDataObject();
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
                return;
            Clipboard.SetDataObject(data, true);
        }
        catch
        {
            // The previous clipboard contents could not be restored.
        }
    }

    private static void SendCtrl(ushort key)
    {
        var inputs = new[]
        {
            Key(0x11, false),
            Key(key, false),
            Key(key, true),
            Key(0x11, true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input Key(ushort vk, bool up) => new()
    {
        Type = 1,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput { Vk = vk, Flags = up ? 2u : 0 }
        }
    };

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

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    private sealed class TinyToast
    {
        private readonly Window _window;
        private readonly TextBlock _text;

        private TinyToast()
        {
            _text = new TextBlock { Foreground = Brushes.White, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            _window = new Window
            {
                Width = 220,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Focusable = false,
                ResizeMode = ResizeMode.NoResize,
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x10, 0x18, 0x28)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(12, 8, 12, 8),
                    Child = _text
                }
            };
            if (GetCursorPos(out var point))
            {
                _window.Left = point.X + 16;
                _window.Top = point.Y + 18;
            }
        }

        public static TinyToast Show(string text)
        {
            var toast = new TinyToast();
            toast._text.Text = text;
            toast._window.Show();
            return toast;
        }

        public void Set(string text) => _text.Text = text;

        public void Close()
        {
            try
            {
                _window.Close();
            }
            catch
            {
                // The toast may already be closed.
            }
        }
    }
}

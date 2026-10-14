using System.Runtime.InteropServices;
using System.Windows;
using Kutu.Core.Text;

namespace Kutu.Sdk.Windows;

public static class KeyInjector
{
    private static readonly System.Collections.Concurrent.BlockingCollection<Action> Queue = new();
    private static int _started;
    private static int _busy;
    private static int _hookDepth;

    public static bool IsBusy => Volatile.Read(ref _busy) != 0;

    public static void EnterHook() => Interlocked.Increment(ref _hookDepth);

    public static void LeaveHook() => Interlocked.Decrement(ref _hookDepth);

    public static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        var thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "Kutu.KeyInject"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static void Run(Action work)
    {
        Start();
        Queue.Add(work);
    }

    public static Task RunAsync(Action work)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Run(() =>
        {
            try
            {
                work();
                done.TrySetResult();
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
        });
        return done.Task;
    }

    public static Task<T> RunAsync<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Run(() =>
        {
            try
            {
                done.TrySetResult(work());
            }
            catch (Exception ex)
            {
                done.TrySetException(ex);
            }
        });
        return done.Task;
    }

    public static InjectResult Send(IReadOnlyList<KeyStroke> strokes)
    {
        if (strokes.Count == 0)
            return new InjectResult(0, 0, 0);

        for (var i = 0; i < 300 && Volatile.Read(ref _hookDepth) != 0; i++)
            Thread.Sleep(1);
        Thread.Sleep(5);

        var accepted = 0;
        for (var offset = 0; offset < strokes.Count;)
        {
            var count = Math.Min(32, strokes.Count - offset);
            var batch = new NativeInput[count];
            for (var i = 0; i < count; i++)
                batch[i] = NativeInput.Keyboard(strokes[offset + i]);

            var sent = (int)SendInput((uint)count, batch, Marshal.SizeOf<NativeInput>());
            if (sent != count)
            {
                var error = Marshal.GetLastWin32Error();
                return new InjectResult(strokes.Count, accepted + Math.Max(sent, 0), error == 0 ? 87 : error);
            }

            accepted += sent;
            offset += count;
        }

        return new InjectResult(strokes.Count, accepted, 0);
    }

    public static string CaptureSelection()
    {
        try
        {
            Clipboard.Clear();
        }
        catch
        {
            // The clipboard can be busy for a moment.
        }

        Send(KeyStrokePlan.Chord(0x11, 0x43));
        for (var i = 0; i < 10; i++)
        {
            Thread.Sleep(40);
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

    public static InjectResult PasteText(string text)
    {
        if (!SetText(text))
            return new InjectResult(4, 0, 1418);
        Thread.Sleep(40);
        return Send(KeyStrokePlan.Chord(0x11, 0x56));
    }

    public static IDataObject? Snapshot()
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

    public static void Restore(IDataObject? data)
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

    public static bool SetText(string text)
    {
        for (var i = 0; i < 8; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                Thread.Sleep(30);
            }
        }

        return false;
    }

    private static void Loop()
    {
        OleInitialize(IntPtr.Zero);
        try
        {
            while (true)
            {
                if (Queue.TryTake(out var work, 30))
                {
                    Interlocked.Increment(ref _busy);
                    try
                    {
                        work();
                    }
                    catch
                    {
                        // The caller logs its own failure.
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _busy);
                    }
                }

                while (PeekMessage(out var message, IntPtr.Zero, 0, 0, 1))
                {
                    TranslateMessage(ref message);
                    DispatchMessage(ref message);
                }
            }
        }
        finally
        {
            OleUninitialize();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, NativeInput[] inputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out Msg message, IntPtr hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg message);

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Id;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }
}

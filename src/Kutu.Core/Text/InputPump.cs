using System.Runtime.InteropServices;

namespace Kutu.Core.Text;

public static class InputPump
{
    public static InjectResult Send(IReadOnlyList<KeyStroke> strokes)
    {
        if (strokes.Count == 0)
            return new InjectResult(0, 0, 0);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, NativeInput[] inputs, int cbSize);
}

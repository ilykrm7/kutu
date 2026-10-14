namespace Kutu.Core.Text;

public readonly struct KeyStroke
{
    public ushort Vk { get; init; }

    public ushort Scan { get; init; }

    public uint Flags { get; init; }

    public nint Extra { get; init; }
}

public static class KeyStrokePlan
{
    public const uint KeyUp = 0x0002;
    public const uint UnicodeFlag = 0x0004;

    public static KeyStroke[] ReleaseModifiers()
    {
        ushort[] keys = [0x10, 0xA0, 0xA1, 0x11, 0xA2, 0xA3, 0x12, 0xA4, 0xA5, 0x5B, 0x5C];
        var strokes = new KeyStroke[keys.Length];
        for (var i = 0; i < keys.Length; i++)
            strokes[i] = new KeyStroke { Vk = keys[i], Flags = KeyUp, Extra = ExpanderHook.InjectionMarker };
        return strokes;
    }

    public static KeyStroke[] Backspaces(int count)
    {
        if (count < 0)
            count = 0;
        var strokes = new KeyStroke[count * 2];
        for (var i = 0; i < count; i++)
        {
            strokes[i * 2] = new KeyStroke { Vk = 0x08, Extra = ExpanderHook.InjectionMarker };
            strokes[(i * 2) + 1] = new KeyStroke { Vk = 0x08, Flags = KeyUp, Extra = ExpanderHook.InjectionMarker };
        }

        return strokes;
    }

    public static KeyStroke[] Unicode(string? text)
    {
        text ??= "";
        var strokes = new KeyStroke[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            var scan = (ushort)text[i];
            strokes[i * 2] = new KeyStroke { Scan = scan, Flags = UnicodeFlag, Extra = ExpanderHook.InjectionMarker };
            strokes[(i * 2) + 1] = new KeyStroke { Scan = scan, Flags = UnicodeFlag | KeyUp, Extra = ExpanderHook.InjectionMarker };
        }

        return strokes;
    }

    public static KeyStroke[] Chord(ushort modifier, ushort key) =>
    [
        new KeyStroke { Vk = modifier, Extra = ExpanderHook.InjectionMarker },
        new KeyStroke { Vk = key, Extra = ExpanderHook.InjectionMarker },
        new KeyStroke { Vk = key, Flags = KeyUp, Extra = ExpanderHook.InjectionMarker },
        new KeyStroke { Vk = modifier, Flags = KeyUp, Extra = ExpanderHook.InjectionMarker }
    ];

    public static KeyStroke[] Replace(int backspaces, string? text)
    {
        var deleted = Backspaces(backspaces);
        var typed = Unicode(text);
        var strokes = new KeyStroke[deleted.Length + typed.Length];
        deleted.CopyTo(strokes, 0);
        typed.CopyTo(strokes, deleted.Length);
        return strokes;
    }
}

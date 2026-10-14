using System.Runtime.InteropServices;

namespace Kutu.Core.Text;

[StructLayout(LayoutKind.Sequential)]
public struct NativeInput
{
    public uint Type;
    public NativeInputUnion Data;

    public static NativeInput Keyboard(KeyStroke stroke) => new()
    {
        Type = 1,
        Data = new NativeInputUnion
        {
            Keyboard = new NativeKeyboardInput
            {
                Vk = stroke.Vk,
                Scan = stroke.Scan,
                Flags = stroke.Flags,
                Extra = stroke.Extra
            }
        }
    };
}

[StructLayout(LayoutKind.Explicit)]
public struct NativeInputUnion
{
    [FieldOffset(0)] public NativeMouseInput Mouse;
    [FieldOffset(0)] public NativeKeyboardInput Keyboard;
    [FieldOffset(0)] public NativeHardwareInput Hardware;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeMouseInput
{
    public int X;
    public int Y;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nint Extra;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeKeyboardInput
{
    public ushort Vk;
    public ushort Scan;
    public uint Flags;
    public uint Time;
    public nint Extra;
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeHardwareInput
{
    public uint Msg;
    public ushort ParamL;
    public ushort ParamH;
}

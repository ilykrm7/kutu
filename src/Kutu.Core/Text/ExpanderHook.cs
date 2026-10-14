namespace Kutu.Core.Text;

public static class ExpanderHook
{
    public const nint InjectionMarker = 0x4B757475;

    public static bool ShouldHandle(uint flags, bool injecting, nint extra = 0)
    {
        // RDP stamps physical keystrokes with LLKHF_INJECTED (0x10) and some
        // accessibility tools use LLKHF_LOWER_IL_INJECTED (0x02). Those are real
        // input. Skip only this process's own injection, marked in dwExtraInfo.
        _ = flags;
        return !injecting && extra != InjectionMarker;
    }
}

namespace Kutu.Core.Text;

public static class ExpanderHook
{
    public static bool ShouldHandle(uint flags, bool injecting)
    {
        // RDP stamps physical keystrokes with LLKHF_INJECTED (0x10) and some
        // accessibility tools use LLKHF_LOWER_IL_INJECTED (0x02). Those are real
        // input. Only keystrokes this process is injecting itself are skipped.
        _ = flags;
        return !injecting;
    }
}

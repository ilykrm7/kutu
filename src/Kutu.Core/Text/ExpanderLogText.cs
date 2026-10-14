namespace Kutu.Core.Text;

public static class ExpanderLogText
{
    public static string? VisibleBuffer(string buffer)
    {
        var index = buffer.LastIndexOf(':');
        return index < 0 ? null : buffer[index..];
    }
}

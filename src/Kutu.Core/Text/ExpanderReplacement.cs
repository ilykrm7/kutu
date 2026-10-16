namespace Kutu.Core.Text;

/// <summary>
/// How many typed characters a snippet must erase before its body is inserted.
/// </summary>
public static class ExpanderReplacement
{
    public static int BackspaceCount(string? trigger)
    {
        var text = (trigger ?? "").Trim();
        return text.Length;
    }
}

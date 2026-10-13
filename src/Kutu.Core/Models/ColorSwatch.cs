namespace Kutu.Core.Models;

public sealed class ColorSwatch
{
    public string Hex { get; set; } = "#000000";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

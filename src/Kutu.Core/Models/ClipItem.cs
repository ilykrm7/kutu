namespace Kutu.Core.Models;

public sealed class ClipItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>text or image</summary>
    public string Kind { get; set; } = "text";

    public string Text { get; set; } = "";

    public string? ImagePath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public bool Pinned { get; set; }
}

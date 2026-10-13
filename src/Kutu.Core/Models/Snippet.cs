namespace Kutu.Core.Models;

public sealed class Snippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Trigger { get; set; } = "";

    public string Body { get; set; } = "";
}

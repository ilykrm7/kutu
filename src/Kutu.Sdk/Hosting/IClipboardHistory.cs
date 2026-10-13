using Kutu.Core.Models;

namespace Kutu.Sdk.Hosting;

public interface IClipboardHistory
{
    IReadOnlyList<ClipItem> Items { get; }

    event EventHandler? Changed;

    void Remove(string id);

    void TogglePin(string id);

    void ClearUnpinned();

    void Copy(ClipItem item);
}

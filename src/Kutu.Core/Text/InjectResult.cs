namespace Kutu.Core.Text;

public readonly record struct InjectResult(int RequestedEvents, int AcceptedEvents, int Win32Error)
{
    public bool Ok => AcceptedEvents == RequestedEvents;

    public int KeyCount => AcceptedEvents / 2;
}

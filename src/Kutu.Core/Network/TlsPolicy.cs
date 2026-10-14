using System.Net;

namespace Kutu.Core.Network;

public static class TlsPolicy
{
    static TlsPolicy() => Enable();

    public static void Enable()
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }
        catch
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
    }
}

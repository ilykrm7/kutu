using System.Security.Cryptography;
using System.Text;
using Kutu.Core.Security;

namespace Kutu.App.Services;

public sealed class DpapiProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "Kutu.v1.dpapi"u8.ToArray();

    public string Protect(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var protectedBytes = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedValue)
    {
        var data = Convert.FromBase64String(protectedValue);
        var plain = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }
}

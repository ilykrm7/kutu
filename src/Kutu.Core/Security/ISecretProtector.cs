namespace Kutu.Core.Security;

public interface ISecretProtector
{
    string Protect(string plain);

    string Unprotect(string protectedValue);
}

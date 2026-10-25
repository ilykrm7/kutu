using System.Security.Cryptography;
using System.Text;

namespace Kutu.Core.Security;

public static class Digests
{
    public static readonly string[] Algorithms = ["MD5", "SHA1", "SHA256", "SHA384", "SHA512"];

    public static string Compute(string algorithm, byte[] data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        using HashAlgorithm hash = algorithm.ToUpperInvariant() switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            "SHA384" => SHA384.Create(),
            "SHA512" => SHA512.Create(),
            _ => throw new ArgumentException("Unknown digest.", nameof(algorithm))
        };
        return ToHex(hash.ComputeHash(data));
    }

    public static byte[] Utf8(string? text) => Encoding.UTF8.GetBytes(text ?? "");

    private static string ToHex(byte[] data)
    {
        var chars = new char[data.Length * 2];
        const string Hex = "0123456789abcdef";
        for (var i = 0; i < data.Length; i++)
        {
            chars[i * 2] = Hex[data[i] >> 4];
            chars[i * 2 + 1] = Hex[data[i] & 0xF];
        }

        return new string(chars);
    }
}

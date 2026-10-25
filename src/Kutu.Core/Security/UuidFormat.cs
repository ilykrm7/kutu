using System.Security.Cryptography;

namespace Kutu.Core.Security;

public static class UuidFormat
{
    public static Guid CreateVersion7(long unixMilliseconds, byte[] random)
    {
        if (random is null)
            throw new ArgumentNullException(nameof(random));
        if (random.Length < 10)
            throw new ArgumentException("UUID v7 needs 10 random bytes.", nameof(random));
        if (unixMilliseconds < 0)
            unixMilliseconds = 0;

        var rfc = new byte[16];
        rfc[0] = (byte)((unixMilliseconds >> 40) & 0xFF);
        rfc[1] = (byte)((unixMilliseconds >> 32) & 0xFF);
        rfc[2] = (byte)((unixMilliseconds >> 24) & 0xFF);
        rfc[3] = (byte)((unixMilliseconds >> 16) & 0xFF);
        rfc[4] = (byte)((unixMilliseconds >> 8) & 0xFF);
        rfc[5] = (byte)(unixMilliseconds & 0xFF);
        rfc[6] = (byte)(0x70 | (random[0] & 0x0F));
        rfc[7] = random[1];
        rfc[8] = (byte)(0x80 | (random[2] & 0x3F));
        rfc[9] = random[3];
        rfc[10] = random[4];
        rfc[11] = random[5];
        rfc[12] = random[6];
        rfc[13] = random[7];
        rfc[14] = random[8];
        rfc[15] = random[9];
        return FromRfcBytes(rfc);
    }

    public static Guid CreateVersion7()
    {
        var random = new byte[10];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);
        return CreateVersion7(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), random);
    }

    public static Guid FromRfcBytes(byte[] rfc)
    {
        if (rfc is null)
            throw new ArgumentNullException(nameof(rfc));
        if (rfc.Length != 16)
            throw new ArgumentException("UUID is 16 bytes.", nameof(rfc));

        var bytes = new byte[16];
        bytes[0] = rfc[3];
        bytes[1] = rfc[2];
        bytes[2] = rfc[1];
        bytes[3] = rfc[0];
        bytes[4] = rfc[5];
        bytes[5] = rfc[4];
        bytes[6] = rfc[7];
        bytes[7] = rfc[6];
        Buffer.BlockCopy(rfc, 8, bytes, 8, 8);
        return new Guid(bytes);
    }

    public static string Format(Guid id, bool upper, bool braces, bool hyphens)
    {
        var text = id.ToString(hyphens ? "D" : "N");
        text = upper ? text.ToUpperInvariant() : text.ToLowerInvariant();
        return braces ? "{" + text + "}" : text;
    }
}

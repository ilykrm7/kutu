using System.Security.Cryptography;

namespace Kutu.Core.IO;

public static class Sha256Check
{
    public static string Compute(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return ToHex(hash);
    }

    public static string Compute(byte[] data)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(data));
    }

    public static string? FindHash(string sumsText, string fileName)
    {
        if (string.IsNullOrWhiteSpace(sumsText))
            return null;

        string? lone = null;
        foreach (var raw in sumsText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;
            var parts = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !IsHex64(parts[0]))
                continue;
            if (parts.Length == 1)
            {
                lone ??= parts[0].ToLowerInvariant();
                continue;
            }

            var name = parts[^1].TrimStart('*');
            if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("\\" + fileName, StringComparison.OrdinalIgnoreCase))
                return parts[0].ToLowerInvariant();
        }

        return lone;
    }

    public static bool Matches(string path, string sumsText, string fileName)
    {
        var expected = FindHash(sumsText, fileName);
        if (expected is null || !File.Exists(path))
            return false;
        var actual = Compute(path);
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHex64(string value)
    {
        if (value.Length != 64)
            return false;
        foreach (var ch in value)
        {
            if (!Uri.IsHexDigit(ch))
                return false;
        }

        return true;
    }

    private static string ToHex(byte[] hash)
    {
        var chars = new char[hash.Length * 2];
        for (var i = 0; i < hash.Length; i++)
        {
            var b = hash[i];
            chars[i * 2] = Hex(b >> 4);
            chars[i * 2 + 1] = Hex(b & 0x0f);
        }

        return new string(chars);
    }

    private static char Hex(int value) => (char)(value < 10 ? '0' + value : 'a' + (value - 10));
}

using System.Security.Cryptography;
using System.Text;
using Kutu.Core.Shell;

namespace Kutu.Core.Tests;

public class AppxPackageTests
{
    [Fact]
    public void Package_hashes_payload_files_and_stores_them_uncompressed()
    {
        var manifest = Encoding.UTF8.GetBytes("<Package />");
        var logo = new byte[] { 1, 2, 3, 4 };
        var package = AppxPackage.Create(new (string, byte[])[]
        {
            ("AppxManifest.xml", manifest),
            ("StoreLogo.png", logo)
        });

        var entries = ReadStored(package);
        Assert.Equal(new[] { "AppxBlockMap.xml", "[Content_Types].xml", "AppxManifest.xml", "StoreLogo.png" }, entries.Select(entry => entry.Name).ToArray());
        Assert.Equal(manifest, entries[2].Data);
        Assert.Equal(logo, entries[3].Data);

        var map = Encoding.UTF8.GetString(entries[0].Data);
        Assert.Contains($"Name=\"AppxManifest.xml\" Size=\"{manifest.Length}\" LfhSize=\"{AppxPackage.LocalHeaderSize("AppxManifest.xml")}\"", map);
        Assert.Contains($"Hash=\"{Hash(manifest)}\"", map);
        Assert.Contains($"Hash=\"{Hash(logo)}\"", map);
        Assert.DoesNotContain("AppxBlockMap.xml\" Size", map);
        Assert.Equal(30 + "[Content_Types].xml".Length, entries[1].HeaderSize);
    }

    private static List<(string Name, byte[] Data, int HeaderSize)> ReadStored(byte[] package)
    {
        var entries = new List<(string Name, byte[] Data, int HeaderSize)>();
        var cursor = 0;
        while (cursor + 4 <= package.Length && BitConverter.ToUInt32(package, cursor) == 0x04034b50)
        {
            var nameLength = BitConverter.ToUInt16(package, cursor + 26);
            var extraLength = BitConverter.ToUInt16(package, cursor + 28);
            var size = BitConverter.ToInt32(package, cursor + 18);
            var headerSize = 30 + nameLength + extraLength;
            var name = Encoding.UTF8.GetString(package, cursor + 30, nameLength);
            var data = new byte[size];
            Buffer.BlockCopy(package, cursor + headerSize, data, 0, size);
            entries.Add((name, data, headerSize));
            cursor += headerSize + size;
        }

        Assert.NotEmpty(entries);
        return entries;
    }

    private static string Hash(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(data));
    }
}

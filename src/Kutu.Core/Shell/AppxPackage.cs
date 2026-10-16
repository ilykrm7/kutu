using System.Security.Cryptography;
using System.Text;

namespace Kutu.Core.Shell;

/// <summary>
/// Unsigned package layout kept for tests. The app does not install it.
/// The block map covers the payload files. The map itself and the content-types part are not hashed.
/// </summary>
public static class AppxPackage
{
    public static byte[] Create(IReadOnlyList<(string Name, byte[] Data)> files)
    {
        if (files.Count == 0)
            throw new ArgumentException("Package needs at least one file.", nameof(files));

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Name) || file.Name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new ArgumentException("Package file names must be a single path segment.", nameof(files));
            if (file.Data is null)
                throw new ArgumentException("Package file data is required.", nameof(files));
        }

        var entries = new List<(string Name, byte[] Data)>
        {
            ("AppxBlockMap.xml", Encoding.UTF8.GetBytes(BlockMap(files))),
            ("[Content_Types].xml", Encoding.UTF8.GetBytes(ContentTypes()))
        };
        entries.AddRange(files);
        return Zip(entries);
    }

    public static int LocalHeaderSize(string name) => 30 + Encoding.UTF8.GetByteCount(name);

    private static string BlockMap(IReadOnlyList<(string Name, byte[] Data)> files)
    {
        var body = new StringBuilder();
        body.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
        body.Append("<BlockMap xmlns=\"http://schemas.microsoft.com/appx/2010/blockmap\" HashMethod=\"http://www.w3.org/2001/04/xmlenc#sha256\">");
        foreach (var file in files)
        {
            body.Append("<File Name=\"").Append(file.Name).Append("\" Size=\"").Append(file.Data.Length);
            body.Append("\" LfhSize=\"").Append(LocalHeaderSize(file.Name)).Append("\">");
            for (var offset = 0; offset < file.Data.Length || offset == 0; offset += 65536)
            {
                var count = Math.Min(65536, file.Data.Length - offset);
                if (count < 0)
                    count = 0;
                body.Append("<Block Hash=\"").Append(Hash(file.Data, offset, count)).Append("\"/>");
                if (file.Data.Length == 0)
                    break;
            }

            body.Append("</File>");
        }

        body.Append("</BlockMap>");
        return body.ToString();
    }

    private static string Hash(byte[] data, int offset, int count)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(data, offset, count));
    }

    private static string ContentTypes() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="png" ContentType="image/png"/><Default Extension="xml" ContentType="application/vnd.ms-appx.manifest+xml"/><Override PartName="/AppxBlockMap.xml" ContentType="application/vnd.ms-appx.blockmap+xml"/><Override PartName="/AppxManifest.xml" ContentType="application/vnd.ms-appx.manifest+xml"/></Types>
        """;

    private static byte[] Zip(IReadOnlyList<(string Name, byte[] Data)> entries)
    {
        using var output = new MemoryStream();
        var central = new List<byte[]>();
        foreach (var entry in entries)
        {
            var name = Encoding.UTF8.GetBytes(entry.Name);
            var crc = Crc32(entry.Data);
            var offset = (uint)output.Length;
            WriteU32(output, 0x04034b50);
            WriteU16(output, 20);
            WriteU16(output, 0);
            WriteU16(output, 0);
            WriteU16(output, 0);
            WriteU16(output, 0x5C21);
            WriteU32(output, crc);
            WriteU32(output, (uint)entry.Data.Length);
            WriteU32(output, (uint)entry.Data.Length);
            WriteU16(output, name.Length);
            WriteU16(output, 0);
            output.Write(name, 0, name.Length);
            output.Write(entry.Data, 0, entry.Data.Length);

            using var header = new MemoryStream();
            WriteU32(header, 0x02014b50);
            WriteU16(header, 20);
            WriteU16(header, 20);
            WriteU16(header, 0);
            WriteU16(header, 0);
            WriteU16(header, 0);
            WriteU16(header, 0x5C21);
            WriteU32(header, crc);
            WriteU32(header, (uint)entry.Data.Length);
            WriteU32(header, (uint)entry.Data.Length);
            WriteU16(header, name.Length);
            WriteU16(header, 0);
            WriteU16(header, 0);
            WriteU16(header, 0);
            WriteU16(header, 0);
            WriteU32(header, 0);
            WriteU32(header, offset);
            header.Write(name, 0, name.Length);
            central.Add(header.ToArray());
        }

        var centralOffset = (uint)output.Length;
        foreach (var header in central)
            output.Write(header, 0, header.Length);
        var centralSize = (uint)(output.Length - centralOffset);
        WriteU32(output, 0x06054b50);
        WriteU16(output, 0);
        WriteU16(output, 0);
        WriteU16(output, entries.Count);
        WriteU16(output, entries.Count);
        WriteU32(output, centralSize);
        WriteU32(output, centralOffset);
        WriteU16(output, 0);
        return output.ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static void WriteU16(Stream stream, int value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
    }

    private static void WriteU32(Stream stream, uint value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }
}

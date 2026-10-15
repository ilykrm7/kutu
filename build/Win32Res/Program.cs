using System.Reflection.PortableExecutable;
using System.Text;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: Win32Res <out.res> <app.manifest> <kutu.ico>");
    return 1;
}

var output = Path.GetFullPath(args[0]);
var manifest = File.ReadAllBytes(args[1]);
var icon = ParseIco(File.ReadAllBytes(args[2]));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);

var resources = new List<WinResource>();
for (var i = 0; i < icon.Count; i++)
    resources.Add(new WinResource(3, (ushort)(i + 1), icon[i].Image));
resources.Add(new WinResource(14, 1, GroupIcon(icon)));
resources.Add(new WinResource(16, 1, VersionInfo()));
resources.Add(new WinResource(24, 1, manifest));
File.WriteAllBytes(output, CoffResource(resources));
using (var check = File.OpenRead(output))
{
    var headers = new PEHeaders(check);
    var sections = headers.SectionHeaders.ToArray();
    var names = sections.Select(header => header.Name).ToArray();
    var directory = sections.FirstOrDefault(header => header.Name == ".rsrc$01");
    var data = sections.FirstOrDefault(header => header.Name == ".rsrc$02");
    if (directory.Name != ".rsrc$01" || data.Name != ".rsrc$02" || sections.Length < 3 || sections[2].Name != ".rsrc$02")
        throw new InvalidOperationException("resource object is missing .rsrc sections: " + string.Join(", ", names));
    if (directory.SizeOfRawData == 0 || data.SizeOfRawData == 0 || directory.PointerToRawData == 0 || data.PointerToRawData == 0)
        throw new InvalidOperationException("resource sections have empty raw data");
    if (directory.PointerToRawData + directory.SizeOfRawData > check.Length || data.PointerToRawData + data.SizeOfRawData > check.Length)
        throw new InvalidOperationException("resource sections point outside the object file");
}

Console.WriteLine($"wrote {new FileInfo(output).Length} bytes of win32 resources");
return 0;

static byte[] CoffResource(List<WinResource> resources)
{
    var ordered = resources.OrderBy(item => item.Type).ThenBy(item => item.Id).ToList();
    using var data = new MemoryStream();
    var dataAt = new int[ordered.Count];
    for (var i = 0; i < ordered.Count; i++)
    {
        Align(data);
        dataAt[i] = (int)data.Position;
        data.Write(ordered[i].Data);
    }

    Align(data);
    var tree = ResourceTree(ordered, out var relocAt);

    var text = new byte[4];
    const int headerSize = 20 + 40 * 3;
    var textAt = Align4(headerSize);
    var treeAt = Align4(textAt + text.Length);
    var relocAtFile = Align4(treeAt + tree.Length);
    var dataFileAt = Align4(relocAtFile + relocAt.Length * 10);
    var symbolAt = Align4(dataFileAt + (int)data.Length);

    using var file = new MemoryStream();
    using var writer = new BinaryWriter(file);
    writer.Write((ushort)0x14C);
    writer.Write((ushort)3);
    writer.Write(0);
    writer.Write(symbolAt);
    writer.Write(ordered.Count);
    writer.Write((ushort)0);
    writer.Write((ushort)0);

    WriteSection(writer, ".text", text.Length, textAt, 0, 0);
    WriteSection(writer, ".rsrc$01", tree.Length, treeAt, relocAtFile, relocAt.Length);
    WriteSection(writer, ".rsrc$02", (int)data.Length, dataFileAt, 0, 0);

    writer.Write(text);
    Pad(writer, treeAt);
    writer.Write(tree);
    Pad(writer, relocAtFile);
    for (var i = 0; i < relocAt.Length; i++)
    {
        writer.Write((uint)relocAt[i]);
        writer.Write((uint)i);
        writer.Write((ushort)7);
    }

    Pad(writer, dataFileAt);
    writer.Write(data.ToArray());
    Pad(writer, symbolAt);
    for (var i = 0; i < ordered.Count; i++)
    {
        writer.Write(Encoding.ASCII.GetBytes(".rsrc$02"));
        writer.Write((uint)dataAt[i]);
        writer.Write((short)3);
        writer.Write((ushort)0);
        writer.Write((byte)3);
        writer.Write((byte)0);
    }

    return file.ToArray();
}

static void WriteSection(BinaryWriter writer, string name, int rawSize, int rawPointer, int relocPointer, int relocCount)
{
    var bytes = new byte[8];
    Encoding.ASCII.GetBytes(name, 0, Math.Min(name.Length, 8), bytes, 0);
    writer.Write(bytes);
    writer.Write(rawSize);
    writer.Write(0);
    writer.Write(rawSize);
    writer.Write(rawPointer);
    writer.Write(relocCount == 0 ? 0 : relocPointer);
    writer.Write(0);
    writer.Write((ushort)relocCount);
    writer.Write((ushort)0);
    writer.Write(0x40000040);
}

static byte[] ResourceTree(List<WinResource> ordered, out int[] relocAt)
{
    var types = ordered.Select(item => item.Type).Distinct().OrderBy(type => type).ToList();
    var rootBytes = 16 + 8 * types.Count;
    var typeBytes = types.Sum(type => 16 + 8 * ordered.Count(item => item.Type == type));
    var langBytes = ordered.Count * (16 + 8);
    var dataBytes = ordered.Count * 16;
    var tree = new byte[rootBytes + typeBytes + langBytes + dataBytes];
    var relocs = new int[ordered.Count];

    void WriteDir(int at, int count)
    {
        BitConverter.TryWriteBytes(tree.AsSpan(at + 12), (ushort)0);
        BitConverter.TryWriteBytes(tree.AsSpan(at + 14), (ushort)count);
    }

    void WriteEntry(int at, int id, int offset, bool directory)
    {
        BitConverter.TryWriteBytes(tree.AsSpan(at), (uint)id);
        BitConverter.TryWriteBytes(tree.AsSpan(at + 4), directory ? (uint)offset | 0x80000000 : (uint)offset);
    }

    WriteDir(0, types.Count);
    var typeAt = rootBytes;
    var langAt = rootBytes + typeBytes;
    var dataAt = rootBytes + typeBytes + langBytes;
    var typeIndex = 0;
    var resourceIndex = 0;
    foreach (var type in types)
    {
        var items = ordered.Where(item => item.Type == type).ToList();
        WriteEntry(16 + typeIndex * 8, type, typeAt, true);
        WriteDir(typeAt, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            WriteEntry(typeAt + 16 + i * 8, items[i].Id, langAt, true);
            WriteDir(langAt, 1);
            WriteEntry(langAt + 16, 0x0409, dataAt, false);
            relocs[resourceIndex] = dataAt;
            BitConverter.TryWriteBytes(tree.AsSpan(dataAt + 4), (uint)items[i].Data.Length);
            dataAt += 16;
            langAt += 24;
            resourceIndex++;
        }

        typeAt += 16 + 8 * items.Count;
        typeIndex++;
    }

    relocAt = relocs;
    return tree;
}

static void Pad(BinaryWriter writer, int position)
{
    while (writer.BaseStream.Position < position)
        writer.Write((byte)0);
}

static int Align4(int value) => (value + 3) & ~3;

static void Align(Stream stream)
{
    while (stream.Position % 4 != 0)
        stream.WriteByte(0);
}

static void AlignWriter(BinaryWriter writer)
{
    var pad = (4 - (writer.BaseStream.Position % 4)) % 4;
    for (var i = 0; i < pad; i++)
        writer.Write((byte)0);
}

static byte[] VersionInfo()
{
    var strings = new (string Key, string Value)[]
    {
        ("CompanyName", "Kerim Ekşi"),
        ("FileDescription", "Kutu — Windows için küçük araçlar."),
        ("FileVersion", "1.1.1.0"),
        ("InternalName", "Kutu.exe"),
        ("LegalCopyright", "Copyright © 2026 Kerim Ekşi"),
        ("OriginalFilename", "Kutu.exe"),
        ("ProductName", "Kutu"),
        ("ProductVersion", "1.1.1")
    };

    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    var root = writer.BaseStream.Position;
    writer.Write((ushort)0);
    writer.Write((ushort)52);
    writer.Write((ushort)0);
    writer.Write(Encoding.Unicode.GetBytes("VS_VERSION_INFO\0"));
    AlignWriter(writer);
    writer.Write(FixedFileInfo());
    AlignWriter(writer);
    var info = writer.BaseStream.Position;
    Begin(writer, 0, "StringFileInfo");
    var table = writer.BaseStream.Position;
    Begin(writer, 0, "040904B0");
    foreach (var pair in strings)
        WriteText(writer, pair.Key, pair.Value);
    End(writer, table);
    End(writer, info);
    var vars = writer.BaseStream.Position;
    Begin(writer, 0, "VarFileInfo");
    WriteBinary(writer, "Translation", [0x09, 0x04, 0xB0, 0x04]);
    End(writer, vars);
    End(writer, root);
    return stream.ToArray();
}

static byte[] FixedFileInfo()
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write(0xFEEF04BD);
    writer.Write(0x00010000);
    writer.Write((uint)0x00010001);
    writer.Write((uint)0x00010000);
    writer.Write((uint)0x00010001);
    writer.Write((uint)0x00010000);
    writer.Write(0x3F);
    writer.Write(0);
    writer.Write(0x00040004);
    writer.Write(1);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    return stream.ToArray();
}

static void WriteText(BinaryWriter writer, string key, string value)
{
    var start = writer.BaseStream.Position;
    var chars = Encoding.Unicode.GetBytes(value + "\0");
    Begin(writer, (ushort)((value.Length + 1)), key);
    writer.Write(chars);
    AlignWriter(writer);
    End(writer, start);
}

static void WriteBinary(BinaryWriter writer, string key, byte[] value)
{
    var start = writer.BaseStream.Position;
    writer.Write((ushort)0);
    writer.Write((ushort)value.Length);
    writer.Write((ushort)0);
    writer.Write(Encoding.Unicode.GetBytes(key + "\0"));
    AlignWriter(writer);
    writer.Write(value);
    AlignWriter(writer);
    End(writer, start);
}

static void Begin(BinaryWriter writer, ushort valueLength, string key)
{
    writer.Write((ushort)0);
    writer.Write(valueLength);
    writer.Write((ushort)1);
    writer.Write(Encoding.Unicode.GetBytes(key + "\0"));
    AlignWriter(writer);
}

static void End(BinaryWriter writer, long start)
{
    AlignWriter(writer);
    var end = writer.BaseStream.Position;
    writer.BaseStream.Position = start;
    writer.Write((ushort)(end - start));
    writer.BaseStream.Position = end;
}

static byte[] GroupIcon(List<IconImage> images)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)images.Count);
    for (var i = 0; i < images.Count; i++)
    {
        var image = images[i];
        var planes = image.Planes;
        var bitCount = image.BitCount;
        if (image.Image.Length >= 16 && BitConverter.ToUInt32(image.Image, 0) == 40)
        {
            planes = BitConverter.ToUInt16(image.Image, 12);
            bitCount = BitConverter.ToUInt16(image.Image, 14);
        }

        writer.Write(image.Width);
        writer.Write(image.Height);
        writer.Write(image.Colors);
        writer.Write(image.Reserved);
        writer.Write(planes);
        writer.Write(bitCount);
        writer.Write((uint)image.Image.Length);
        writer.Write((ushort)(i + 1));
    }

    return stream.ToArray();
}

static List<IconImage> ParseIco(byte[] file)
{
    using var stream = new MemoryStream(file);
    using var reader = new BinaryReader(stream);
    _ = reader.ReadUInt16();
    var kind = reader.ReadUInt16();
    if (kind != 1)
        throw new InvalidOperationException("kutu.ico is not an icon");
    var count = reader.ReadUInt16();
    var entries = new List<(byte Width, byte Height, byte Colors, byte Reserved, ushort Planes, ushort BitCount, uint Bytes, uint Offset)>();
    for (var i = 0; i < count; i++)
    {
        entries.Add((
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadByte(),
            reader.ReadUInt16(),
            reader.ReadUInt16(),
            reader.ReadUInt32(),
            reader.ReadUInt32()));
    }

    var images = new List<IconImage>();
    foreach (var entry in entries)
    {
        stream.Position = entry.Offset;
        images.Add(new IconImage(
            entry.Width,
            entry.Height,
            entry.Colors,
            entry.Reserved,
            entry.Planes,
            entry.BitCount,
            reader.ReadBytes(checked((int)entry.Bytes))));
    }

    return images;
}

sealed record IconImage(byte Width, byte Height, byte Colors, byte Reserved, ushort Planes, ushort BitCount, byte[] Image);

sealed record WinResource(ushort Type, ushort Id, byte[] Data);

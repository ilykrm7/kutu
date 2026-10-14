using System.IO.Compression;
using Mono.Cecil;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("usage: UnifyRefs <Kutu.exe>");
    return 1;
}

var path = Path.GetFullPath(args[0]);
var rewritten = Rewrite(path);
Verify(path);
var info = new FileInfo(path);
Console.WriteLine($"unified {rewritten} reference(s) in {info.Name} ({info.Length} bytes)");
return 0;

static int Rewrite(string path)
{
    var read = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true };
    using var module = ModuleDefinition.ReadModule(path, read);
    var embedded = ReadEmbedded(module);
    var canonical = CanonicalVersions(embedded);
    var changes = 0;

    foreach (var item in embedded)
    {
        var count = Retarget(item.Assembly, canonical, item.ResourceName);
        item.Dirty = count > 0;
        changes += count;
    }

    changes += Retarget(module.Assembly, canonical, module.Name);

    if (changes == 0)
        return 0;

    var replaced = new List<EmbeddedResource>();
    foreach (var item in embedded)
    {
        if (!item.Dirty)
            continue;
        var payload = WriteAssembly(item.Assembly);
        if (payload.Length < 64 || payload[0] != (byte)'M' || payload[1] != (byte)'Z')
            throw new InvalidOperationException($"{item.ResourceName} rewrite is not a PE ({payload.Length} bytes)");
        var stored = item.Compressed ? Deflate(payload) : payload;
        var roundTrip = item.Compressed ? Inflate(stored) : stored;
        if (roundTrip.Length != payload.Length || roundTrip[0] != (byte)'M')
            throw new InvalidOperationException($"{item.ResourceName} deflate round-trip failed ({payload.Length} -> {roundTrip.Length})");
        replaced.Add(new EmbeddedResource(item.ResourceName, item.Attributes, stored));
    }

    foreach (var item in embedded.Where(item => item.Dirty))
        module.Resources.Remove(item.Resource);

    foreach (var resource in replaced)
        module.Resources.Add(resource);

    RequireLoader(module);
    var temp = path + ".tmp";
    module.Write(temp);
    File.Move(temp, path, overwrite: true);
    return changes;
}

static void Verify(string path)
{
    var read = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true };
    using var module = ModuleDefinition.ReadModule(path, read);
    RequireLoader(module);
    RequireManifest(path);

    var embedded = ReadEmbedded(module);
    var canonical = CanonicalVersions(embedded);
    var leftovers = new List<string>();
    foreach (var item in embedded)
        leftovers.AddRange(Mismatches(item.Assembly, canonical, item.ResourceName));
    leftovers.AddRange(Mismatches(module.Assembly, canonical, module.Name));
    if (leftovers.Count > 0)
        throw new InvalidOperationException(string.Join(Environment.NewLine, leftovers));
}

static void RequireManifest(string path)
{
    var bytes = File.ReadAllBytes(path);
    if (!ContainsAscii(bytes, "dpiAwareness") || !ContainsAscii(bytes, "longPathAware"))
        throw new InvalidOperationException("Kutu.exe lost its application manifest");
}

static bool ContainsAscii(byte[] haystack, string needle)
{
    var target = System.Text.Encoding.ASCII.GetBytes(needle);
    for (var i = 0; i + target.Length <= haystack.Length; i++)
    {
        var match = true;
        for (var j = 0; j < target.Length; j++)
        {
            if (haystack[i + j] != target[j])
            {
                match = false;
                break;
            }
        }

        if (match)
            return true;
    }

    return false;
}

static void RequireLoader(ModuleDefinition module)
{
    if (module.Types.All(type => type.FullName != "Costura.AssemblyLoader"))
        throw new InvalidOperationException("Costura.AssemblyLoader is missing");
    var moduleType = module.Types.First(type => type.Name == "<Module>");
    if (moduleType.Methods.All(method => method.Name != ".cctor"))
        throw new InvalidOperationException("Costura module initializer is missing");
}

static List<EmbeddedAssembly> ReadEmbedded(ModuleDefinition module)
{
    var list = new List<EmbeddedAssembly>();
    foreach (var resource in module.Resources.ToList())
    {
        if (resource is not EmbeddedResource embedded)
            continue;
        var name = embedded.Name;
        if (!name.StartsWith("costura.", StringComparison.Ordinal) || !name.Contains(".dll", StringComparison.Ordinal))
            continue;

        var compressed = name.EndsWith(".compressed", StringComparison.Ordinal);
        byte[] payload;
        try
        {
            var stored = embedded.GetResourceData();
            payload = compressed ? Inflate(stored) : stored;
        }
        catch (InvalidDataException)
        {
            continue;
        }

        if (payload.Length < 2 || payload[0] != (byte)'M' || payload[1] != (byte)'Z')
            continue;

        AssemblyDefinition assembly;
        try
        {
            assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(payload));
        }
        catch (BadImageFormatException)
        {
            continue;
        }

        list.Add(new EmbeddedAssembly(embedded, name, embedded.Attributes, compressed, assembly));
    }

    return list;
}

static Dictionary<string, AssemblyNameReference> CanonicalVersions(List<EmbeddedAssembly> embedded)
{
    var map = new Dictionary<string, AssemblyNameReference>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in embedded)
    {
        var name = item.Assembly.Name;
        if (!map.TryGetValue(name.Name, out var existing))
        {
            map.Add(name.Name, name);
            continue;
        }

        if (existing.Version != name.Version || !SameToken(existing, name))
            throw new InvalidOperationException($"embedded {name.Name} has conflicting versions {existing.Version} and {name.Version}");
    }

    return map;
}

static int Retarget(AssemblyDefinition assembly, Dictionary<string, AssemblyNameReference> canonical, string label)
{
    var changes = 0;
    foreach (var reference in assembly.MainModule.AssemblyReferences)
    {
        if (!canonical.TryGetValue(reference.Name, out var target))
            continue;
        if (reference.Version == target.Version)
            continue;
        if (!SameToken(reference, target))
            continue;

        Console.WriteLine($"{label}: {reference.Name} {reference.Version} -> {target.Version}");
        reference.Version = target.Version;
        changes++;
    }

    return changes;
}

static IEnumerable<string> Mismatches(AssemblyDefinition assembly, Dictionary<string, AssemblyNameReference> canonical, string label)
{
    foreach (var reference in assembly.MainModule.AssemblyReferences)
    {
        if (!canonical.TryGetValue(reference.Name, out var target))
            continue;
        if (reference.Version == target.Version || !SameToken(reference, target))
            continue;
        yield return $"{label} still references {reference.Name} {reference.Version}, embedded is {target.Version}";
    }
}

static bool SameToken(AssemblyNameReference left, AssemblyNameReference right)
{
    var a = left.PublicKeyToken ?? [];
    var b = right.PublicKeyToken ?? [];
    return a.Length == 0 || b.Length == 0 || a.AsSpan().SequenceEqual(b);
}

static byte[] WriteAssembly(AssemblyDefinition assembly)
{
    var stream = new MemoryStream();
    assembly.Write(stream);
    return stream.ToArray();
}

static byte[] Inflate(byte[] compressed)
{
    using var input = new MemoryStream(compressed);
    using var deflate = new DeflateStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream();
    deflate.CopyTo(output);
    return output.ToArray();
}

static byte[] Deflate(byte[] payload)
{
    using var output = new MemoryStream();
    using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        deflate.Write(payload, 0, payload.Length);
    return output.ToArray();
}

sealed class EmbeddedAssembly(
    EmbeddedResource resource,
    string resourceName,
    ManifestResourceAttributes attributes,
    bool compressed,
    AssemblyDefinition assembly)
{
    public EmbeddedResource Resource { get; } = resource;
    public string ResourceName { get; } = resourceName;
    public ManifestResourceAttributes Attributes { get; } = attributes;
    public bool Compressed { get; } = compressed;
    public AssemblyDefinition Assembly { get; } = assembly;
    public bool Dirty { get; set; }
}

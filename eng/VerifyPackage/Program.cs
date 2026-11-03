using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length != 3)
{
    throw new ArgumentException("Usage: VerifyPackage <package-directory> <version> <git-revision>");
}

string packageDirectory = Path.GetFullPath(args[0]);
string version = args[1];
string revision = args[2];
using var package = ZipFile.OpenRead(Path.Combine(packageDirectory, $"Glean.{version}.nupkg"));
using var symbols = ZipFile.OpenRead(Path.Combine(packageDirectory, $"Glean.{version}.snupkg"));
ValidateManifest(package, symbolPackage: false);
ValidateManifest(symbols, symbolPackage: true);
Require(Read(package, "LICENSE").SequenceEqual(File.ReadAllBytes("LICENSE")), "Packaged license differs from the repository.");
Require(Read(package, "README.md").SequenceEqual(File.ReadAllBytes("README.md")), "Packaged README differs from the repository.");
foreach (string framework in new[] { "net8.0", "net10.0" })
{
    using var xml = new MemoryStream(Read(package, $"lib/{framework}/Glean.xml"));
    var documentation = XDocument.Load(xml);
    Require(documentation.Root?.Element("assembly")?.Element("name")?.Value == "Glean", "XML documentation has the wrong assembly.");
    Require(documentation.Descendants("member").Any(element => (string?)element.Attribute("name") == "T:Glean.AssemblyScope"), "Public XML documentation is empty.");
    ValidateSymbols(framework, Read(package, $"lib/{framework}/Glean.dll"), Read(symbols, $"lib/{framework}/Glean.pdb"));
}

string scratch = Path.Combine(Path.GetTempPath(), "glean-package-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
bool verified = false;
try
{
    string feed = Path.Combine(scratch, "feed");
    Directory.CreateDirectory(feed);
    File.Copy(Path.Combine(packageDirectory, $"Glean.{version}.nupkg"), Path.Combine(feed, $"Glean.{version}.nupkg"));
    CopyDependencies(feed);
    string configuration = Path.Combine(scratch, "NuGet.Config");
    new XDocument(new XElement("configuration", new XElement("packageSources", new XElement("clear"),
        new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed))))).Save(configuration);
    string project = Path.Combine(scratch, "Consumer.csproj");
    new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
        new XElement("PropertyGroup", new XElement("TargetFrameworks", "net8.0;net9.0;net10.0"),
            new XElement("OutputType", "Exe"), new XElement("UseAppHost", "false"),
            new XElement("ImplicitUsings", "enable"), new XElement("Nullable", "enable")),
        new XElement("ItemGroup", new XElement("PackageReference", new XAttribute("Include", "Glean"),
            new XAttribute("Version", $"[{version}]"))))).Save(project);
    File.WriteAllText(Path.Combine(scratch, "Consumer.cs"), """
        using System.Reflection;
        using System.Reflection.Metadata;
        using System.Runtime.InteropServices;
        using System.Runtime.Versioning;

        using Glean;

        int expectedRuntime = int.Parse(args[0]);
        string expectedAsset = ".NETCoreApp,Version=v" + args[1] + ".0";
        var assembly = typeof(AssemblyScope).Assembly;
        if (Environment.Version.Major != expectedRuntime)
        {
            throw new InvalidOperationException("Consumer ran on the wrong runtime.");
        }
        if (assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName != expectedAsset)
        {
            throw new InvalidOperationException("Consumer loaded the wrong Glean target.");
        }
        using var scope = AssemblyScope.Open(assembly.Location);
        int count = 0;
        bool foundScope = false;
        foreach (var type in scope.Context.EnumerateTypes())
        {
            if (type.IsPublic)
            {
                count++;
                foundScope |= type.NameIs("AssemblyScope");
            }
        }
        if ((count == 0) || !foundScope)
        {
            throw new InvalidOperationException("Package traversal did not find its public entry point.");
        }
        Console.WriteLine($"ConsumerRuntime={RuntimeInformation.FrameworkDescription} GleanTarget={expectedAsset} PublicTypes={count} SRMPath={typeof(MetadataReader).Assembly.Location}");
        """);
    Run("restore", project, "--configfile", configuration, "--packages", Path.Combine(scratch, "packages"), "--no-cache", "-p:NuGetAudit=false");
    Run("build", project, "-c", "Release", "--no-restore", "--nologo");
    using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(scratch, "obj", "project.assets.json")));
    foreach (string framework in new[] { "net8.0", "net9.0", "net10.0" })
    {
        string assetFramework = framework == "net9.0" ? "net8.0" : framework;
        var dependency = assets.RootElement.GetProperty("targets").GetProperty(framework).GetProperty($"Glean/{version}");
        Require(dependency.GetProperty("type").GetString() == "package", "Consumer did not resolve Glean as a package.");
        Require(dependency.GetProperty("compile").TryGetProperty($"lib/{assetFramework}/Glean.dll", out _), "Consumer selected the wrong framework asset.");
        byte[] restored = File.ReadAllBytes(Path.Combine(scratch, "packages", "glean", version, "lib", assetFramework, "Glean.dll"));
        Require(SHA256.HashData(restored).SequenceEqual(SHA256.HashData(Read(package, $"lib/{assetFramework}/Glean.dll"))), "Consumer assembly differs from the produced package.");
        Run("exec", Path.Combine(scratch, "bin", "Release", framework, "Consumer.dll"), framework[3..].Split('.')[0], assetFramework[3..].Split('.')[0]);
    }
    Console.WriteLine("Verified both package assets and isolated .NET 8, .NET 9 and .NET 10 consumers, including runtime traversal.");
    verified = true;
}
finally
{
    if (verified)
    {
        Directory.Delete(scratch, recursive: true);
    }
    else
    {
        Console.Error.WriteLine($"Consumer verification files retained at {scratch}");
    }
}

void ValidateManifest(ZipArchive archive, bool symbolPackage)
{
    using var stream = new MemoryStream(Read(archive, "Glean.nuspec"));
    var document = XDocument.Load(stream);
    XNamespace ns = document.Root!.Name.Namespace;
    var metadata = document.Root.Element(ns + "metadata")!;
    Require(metadata.Element(ns + "id")?.Value == "Glean", "Package ID differs.");
    Require(metadata.Element(ns + "version")?.Value == version, "Package version differs.");
    if (!symbolPackage)
    {
        Require(metadata.Element(ns + "authors")?.Value == "Philip Klatt", "Package author differs.");
        Require(metadata.Element(ns + "license")?.Value == "MIT", "Package license differs.");
        Require(metadata.Element(ns + "readme")?.Value == "README.md", "Package README metadata differs.");
    }
    var repository = metadata.Element(ns + "repository");
    Require((string?)repository?.Attribute("url") == "https://github.com/ptklatt/glean", "Repository URL differs.");
    Require((string?)repository?.Attribute("commit") == revision, "Repository revision differs.");
    var groups = metadata.Element(ns + "dependencies")!.Elements(ns + "group").ToArray();
    Require(groups.Select(group => (string?)group.Attribute("targetFramework")).Order().SequenceEqual(new[] { "net10.0", "net8.0" }), "Package framework groups differ.");
    foreach (var group in groups)
    {
        if ((string?)group.Attribute("targetFramework") == "net10.0")
        {
            Require(!group.Elements(ns + "dependency").Any(), ".NET 10 should use framework SRM.");
            continue;
        }

        var dependency = group.Elements(ns + "dependency").Single();
        Require((string?)dependency.Attribute("id") == "System.Reflection.Metadata", "Unexpected package dependency.");
        Require((string?)dependency.Attribute("version") == "10.0.5", "SRM dependency version differs.");
    }
}

void ValidateSymbols(string framework, byte[] assembly, byte[] symbolsBytes)
{
    using var pe = new PEReader(new MemoryStream(assembly));
    var metadata = pe.GetMetadataReader();
    var target = metadata.GetAssemblyDefinition().GetCustomAttributes().Select(metadata.GetCustomAttribute).Single(attribute =>
    {
        var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        return metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent).Name) == "TargetFrameworkAttribute";
    });
    var targetValue = metadata.GetBlobReader(target.Value);
    Require(targetValue.ReadUInt16() == 1, "Target framework attribute is malformed.");
    Require(targetValue.ReadSerializedString() == ".NETCoreApp,Version=v" + framework[3..], "Assembly targets the wrong framework.");
    using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(symbolsBytes));
    var pdb = provider.GetMetadataReader();
    var debugEntries = pe.ReadDebugDirectory();
    var codeViewEntry = debugEntries.Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
    var codeView = pe.ReadCodeViewDebugDirectoryData(codeViewEntry);
    var contentId = new BlobContentId(pdb.DebugMetadataHeader!.Id);
    Require((codeView.Guid == contentId.Guid) && (codeViewEntry.Stamp == contentId.Stamp), "Symbol file does not match the assembly.");
    Require(debugEntries.Any(entry => entry.Type == DebugDirectoryEntryType.Reproducible), "Assembly lacks deterministic build metadata.");
    var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
    var sourceLink = pdb.CustomDebugInformation.Select(pdb.GetCustomDebugInformation).Single(info => pdb.GetGuid(info.Kind) == sourceLinkKind);
    using var links = JsonDocument.Parse(pdb.GetBlobBytes(sourceLink.Value));
    var mappings = links.RootElement.GetProperty("documents").EnumerateObject().ToArray();
    const string repositoryPrefix = "https://raw.githubusercontent.com/ptklatt/glean/";
    string revisionPath = revision + "/";
    Require((mappings.Length > 0) && mappings.All(mapping =>
    {
        string url = mapping.Value.GetString()!;
        return url.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase)
            && url[repositoryPrefix.Length..].StartsWith(revisionPath, StringComparison.Ordinal);
    }), "SourceLink does not identify the expected repository revision.");
    var embeddedKind = new Guid("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
    Require(pdb.Documents.Count > 0, "Symbols contain no source documents.");
    foreach (var handle in pdb.Documents)
    {
        var document = pdb.GetDocument(handle);
        string documentName = pdb.GetString(document.Name).Replace('\\', '/');
        string repositoryRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
        string relativePath;
        if (documentName.StartsWith("/_/", StringComparison.Ordinal))
        {
            relativePath = documentName[3..];
        }
        else if (documentName.StartsWith(repositoryRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            relativePath = documentName[repositoryRoot.Length..];
        }
        else
        {
            throw new InvalidDataException("A source document is outside the repository source root.");
        }
        string resolved = ResolveSourceLink(mappings, documentName);
        Require(resolved.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase)
            && (resolved[repositoryPrefix.Length..] == revisionPath + relativePath), "SourceLink resolves a document to the wrong repository path.");
        var embedded = pdb.GetCustomDebugInformation(handle).Select(pdb.GetCustomDebugInformation).Single(info => pdb.GetGuid(info.Kind) == embeddedKind);
        using var input = new MemoryStream(pdb.GetBlobBytes(embedded.Value));
        using var header = new BinaryReader(input);
        int length = header.ReadInt32();
        using var source = new MemoryStream();
        if (length == 0)
        {
            input.CopyTo(source);
        }
        else
        {
            using var compressed = new DeflateStream(input, CompressionMode.Decompress);
            compressed.CopyTo(source);
            Require(source.Length == length, "Embedded source length differs.");
        }
        Require(pdb.GetGuid(document.HashAlgorithm) == new Guid("8829d00f-11b8-4213-878b-770e8597ac16"), "Unexpected source hash algorithm.");
        Require(SHA256.HashData(source.ToArray()).SequenceEqual(pdb.GetBlobBytes(document.Hash)), "Embedded source checksum differs.");
    }
}

static string ResolveSourceLink(JsonProperty[] mappings, string documentName)
{
    foreach (var mapping in mappings.OrderByDescending(mapping => mapping.Name.Length))
    {
        string key = mapping.Name.Replace('\\', '/');
        string value = mapping.Value.GetString()!;
        int wildcard = key.IndexOf('*');
        if (wildcard < 0)
        {
            if (key == documentName)
            {
                Require(!value.Contains('*'), "SourceLink has an unmatched URL wildcard.");
                return value;
            }
        }
        else
        {
            Require((wildcard == key.Length - 1) && (value.IndexOf('*') == value.Length - 1), "Unexpected SourceLink wildcard pattern.");
            string start = key[..wildcard];
            if (documentName.StartsWith(start, StringComparison.Ordinal))
            {
                return value[..^1] + documentName[start.Length..];
            }
        }
    }
    throw new InvalidDataException("SourceLink mapping is missing for a source document.");
}

void CopyDependencies(string feed)
{
    using var assets = JsonDocument.Parse(File.ReadAllText("src/Glean/obj/project.assets.json"));
    var folders = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(folder => folder.Name).ToArray();
    var paths = assets.RootElement.GetProperty("libraries").EnumerateObject()
        .Where(library => library.Value.GetProperty("type").GetString() == "package")
        .Select(library => library.Value.GetProperty("path").GetString()!).ToList();
    foreach (string projectAssets in new[] { "src/Glean/obj/project.assets.json", "test/Glean.Tests/obj/project.assets.json" })
    {
        using var build = JsonDocument.Parse(File.ReadAllText(projectAssets));
        foreach (var framework in build.RootElement.GetProperty("project").GetProperty("frameworks").EnumerateObject())
        {
            if (framework.Value.TryGetProperty("downloadDependencies", out var downloads))
            {
                paths.AddRange(downloads.EnumerateArray().Select(download => download.GetProperty("name").GetString()!.ToLowerInvariant() + "/" + download.GetProperty("version").GetString()!.Trim('[', ']').Split(',')[0].Trim()));
            }
        }
    }
    foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        string name = path.Replace('/', '.') + ".nupkg";
        string? source = folders.Select(folder => Path.Combine(folder, path, name)).FirstOrDefault(File.Exists);
        if (source != null)
        {
            File.Copy(source, Path.Combine(feed, name));
        }
    }
}

static byte[] Read(ZipArchive archive, string path)
{
    using var input = (archive.GetEntry(path) ?? throw new InvalidDataException($"Missing package entry: {path}")).Open();
    using var output = new MemoryStream();
    input.CopyTo(output);
    return output.ToArray();
}

static void Run(params string[] arguments)
{
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    foreach (string argument in arguments)
    {
        start.ArgumentList.Add(argument);
    }
    using var process = Process.Start(start)!;
    process.WaitForExit();
    Require(process.ExitCode == 0, $"dotnet {arguments[0]} failed.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidDataException(message);
    }
}

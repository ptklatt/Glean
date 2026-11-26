using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyLoaderFileNameCasingTests
{
    public static IEnumerable<object[]> FileNames()
    {
        yield return ["Dependency", "dependency.dll"];
        yield return ["Dependency", "DEPENDENCY.dll"];
        yield return ["Dependency", "Dependency.DLL"];
        yield return ["dependency", "Dependency.dll"];
        yield return ["Dependency", "dependency.exe"];
        yield return ["Dependency", "DEPENDENCY.EXE"];
    }

    public static IEnumerable<object[]> ClosureFileNames()
    {
        foreach (var values in FileNames())
        {
            yield return [values[0], values[1], false];
            yield return [values[0], values[1], true];
        }
    }

    public static IEnumerable<object[]> IdentityCases()
    {
        foreach (string mismatch in new[] { "name", "version", "culture", "key" })
        {
            foreach (bool asynchronous in new[] { false, true })
            {
                yield return [mismatch, asynchronous, false];
                yield return [mismatch, asynchronous, true];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ClosureFileNames))]
    public async Task LoadWithClosure_DifferentFileNameCasing_ResolvesDependency(
        string referenceName, string fileName, bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string references = workspace.CreateDirectory("references");
        string entry = WriteEntry(workspace, referenceName);
        Guid expected = WriteAssembly(Path.Combine(references, fileName));
        using var loader = new AssemblyLoader(references);

        await AssertLoaded(loader, entry, asynchronous, expected);
    }

    [Theory]
    [MemberData(nameof(FileNames))]
    public void DirectoryResolver_DifferentFileNameCasing_ResolvesDependency(string referenceName, string fileName)
    {
        using var workspace = new TemporaryDirectory();
        string references = workspace.CreateDirectory("references");
        string entry = WriteEntry(workspace, referenceName);
        Guid expected = WriteAssembly(Path.Combine(references, fileName));
        using var scope = AssemblyScope.Open(entry);
        using var resolver = new DirectoryAssemblyResolver(references);

        Assert.True(resolver.TryResolve(Assert.Single(scope.Reader.AssemblyReferences), scope.Reader, out var reader));
        Assert.Equal(expected, reader!.GetGuid(reader.GetModuleDefinition().Mvid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_ExactFileNamePreferenceFollowsFileSystemCasing(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string earlier = workspace.CreateDirectory("earlier");
        string later = workspace.CreateDirectory("later");
        string entry = WriteEntry(workspace);
        Guid variant = WriteAssembly(Path.Combine(earlier, "dependency.dll"));
        Guid exact = WriteAssembly(Path.Combine(later, "Dependency.dll"));
        using var loader = new AssemblyLoader(earlier, later);

        await AssertLoaded(loader, entry, asynchronous, OperatingSystem.IsWindows() ? variant : exact);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoadWithClosure_CaseVariantsUseOrdinalOrderRegardlessOfCreationOrder(
        bool asynchronous, bool reverseCreation)
    {
        using var workspace = new TemporaryDirectory();
        string references = workspace.CreateDirectory("references");
        string entry = WriteEntry(workspace);
        string upper = Path.Combine(references, "DEPENDENCY.dll");
        string lower = Path.Combine(references, "dependency.dll");
        if ((reverseCreation) && !OperatingSystem.IsWindows())
        {
            WriteAssembly(lower);
        }

        Guid expected = WriteAssembly(upper);
        if ((!reverseCreation) && !OperatingSystem.IsWindows())
        {
            WriteAssembly(lower);
        }

        using var loader = new AssemblyLoader(references);
        await AssertLoaded(loader, entry, asynchronous, expected);
    }

    [Theory]
    [MemberData(nameof(IdentityCases))]
    public async Task LoadWithClosure_CaseVariantsStillRequireFullIdentity(
        string mismatch, bool asynchronous, bool includeExactIdentity)
    {
        using var workspace = new TemporaryDirectory();
        string wrong = workspace.CreateDirectory("wrong");
        string right = workspace.CreateDirectory("right");
        string entry = WriteEntry(workspace);
        string wrongPath = Path.Combine(wrong, "DEPENDENCY.dll");
        WriteAssembly(wrongPath,
            name: mismatch == "name" ? "Other" : "Dependency",
            version: mismatch == "version" ? 2 : 1,
            culture: mismatch == "culture" ? "fr-FR" : "",
            key: mismatch == "key" ? typeof(object).Assembly.GetName().GetPublicKey() : null);
        using var loader = new AssemblyLoader(wrong, right);
        if (includeExactIdentity)
        {
            Guid expected = WriteAssembly(Path.Combine(right, "dependency.dll"));
            await AssertLoaded(loader, entry, asynchronous, expected);
        }
        else
        {
            var failures = new List<AssemblyDependencyLoadFailure>();
            var loaded = asynchronous
                ? await loader.LoadWithClosureAsync(entry, failures.Add)
                : loader.LoadWithClosure(entry, failures.Add);
            using var set = loaded.Set;
            var failure = Assert.Single(failures);
            Assert.Equal(AssemblyDependencyLoadFailureKind.IdentityMismatch, failure.Kind);
            Assert.True(string.Equals(wrongPath, failure.Path,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            Assert.Same(loaded.EntryReader, Assert.Single(set.RegisteredAssemblies));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_CaseVariantExactIdentityBeatsExactFileNameWithWrongVersion(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string earlier = workspace.CreateDirectory("earlier");
        string later = workspace.CreateDirectory("later");
        string entry = WriteEntry(workspace);
        WriteAssembly(Path.Combine(earlier, "Dependency.dll"), version: 2);
        Guid expected = WriteAssembly(Path.Combine(later, "dependency.dll"));
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            SearchPaths = [earlier, later],
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible
        });

        var substitutions = await AssertLoaded(loader, entry, asynchronous, expected);
        Assert.Empty(substitutions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_EarlierCaseVariantKeepsSearchPathPriority(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string earlier = workspace.CreateDirectory("earlier");
        string later = workspace.CreateDirectory("later");
        string entry = WriteEntry(workspace);
        Guid expected = WriteAssembly(Path.Combine(earlier, "dependency.dll"), version: 2);
        WriteAssembly(Path.Combine(later, "Dependency.dll"), version: 3);
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            SearchPaths = [earlier, later],
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible
        });

        var substitutions = await AssertLoaded(loader, entry, asynchronous, expected);
        Assert.Contains("Version=2.0.0.0", Assert.Single(substitutions).ResolvedIdentity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_OnlyCaseVariantWithDifferentVersion_RecordsSubstitution(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string references = workspace.CreateDirectory("references");
        string entry = WriteEntry(workspace);
        string path = Path.Combine(references, "dependency.dll");
        Guid expected = WriteAssembly(path, version: 2);
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            SearchPaths = [references],
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible
        });

        var substitutions = await AssertLoaded(loader, entry, asynchronous, expected);
        var substitution = Assert.Single(substitutions);
        Assert.Contains("Version=1.0.0.0", substitution.RequestedIdentity);
        Assert.Contains("Version=2.0.0.0", substitution.ResolvedIdentity);
        Assert.True(string.Equals(path, substitution.Path,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("lib/net8.0", false)]
    [InlineData("lib/net8.0", true)]
    [InlineData("ref/net8.0", false)]
    [InlineData("ref/net8.0", true)]
    [InlineData("runtimes/linux-x64/lib/net8.0", false)]
    [InlineData("runtimes/linux-x64/lib/net8.0", true)]
    public async Task LoadWithClosure_CaseVariantInPackageLayout_ResolvesDependency(string layout, bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string package = workspace.CreateDirectory("package");
        string target = Path.Combine(package, layout);
        Directory.CreateDirectory(target);
        Guid expected = WriteAssembly(Path.Combine(target, "dependency.DLL"));
        string entry = WriteEntry(workspace);
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            SearchPaths = [package],
            TargetFrameworkMoniker = "net8.0"
        });

        await AssertLoaded(loader, entry, asynchronous, expected);
    }

    private static async Task<AssemblyBindingSubstitution[]> AssertLoaded(
        AssemblyLoader loader, string entry, bool asynchronous, Guid expectedModule)
    {
        var failures = new List<AssemblyDependencyLoadFailure>();
        var loaded = asynchronous
            ? await loader.LoadWithClosureAsync(entry, failures.Add)
            : loader.LoadWithClosure(entry, failures.Add);
        using var set = loaded.Set;
        Assert.Empty(failures);
        Assert.True(set.TryResolveType(loaded.EntryReader, Assert.Single(loaded.EntryReader.TypeReferences),
            out var reader, out var handle));
        Assert.Equal(expectedModule, reader.GetGuid(reader.GetModuleDefinition().Mvid));
        Assert.Equal("DependencyType", reader.GetString(reader.GetTypeDefinition(handle).Name));
        return set.BindingSubstitutions.ToArray();
    }

    private static string WriteEntry(TemporaryDirectory workspace, string referenceName = "Dependency")
    {
        string path = Path.Combine(workspace.CreateDirectory("entry"), "Entry.dll");
        WriteAssembly(path, name: "Entry", referenceName: referenceName);
        return path;
    }

    private static Guid WriteAssembly(string path, string name = "Dependency", int version = 1,
        string culture = "", byte[]? key = null, string? referenceName = null)
    {
        Guid moduleId = Guid.NewGuid();
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(Path.GetFileName(path)), metadata.GetOrAddGuid(moduleId), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(version, 0, 0, 0),
            metadata.GetOrAddString(culture), key == null ? default : metadata.GetOrAddBlob(key),
            key == null ? 0 : AssemblyFlags.PublicKey, AssemblyHashAlgorithm.None);
        if (referenceName != null)
        {
            var reference = metadata.AddAssemblyReference(metadata.GetOrAddString(referenceName),
                new Version(1, 0, 0, 0), default, default, 0, default);
            metadata.AddTypeReference(reference, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("DependencyType"));
        }

        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"),
            metadata.GetOrAddString("DependencyType"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var image = new BlobBuilder();
        new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata),
            new BlobBuilder(), flags: CorFlags.ILOnly).Serialize(image);
        File.WriteAllBytes(path, image.ToArray());
        return moduleId;
    }
}

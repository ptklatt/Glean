using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyLoaderPathComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadFile_CaseDistinctPathsUseFileSystemIdentity(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var upperPath = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Sample",
            "namespace Fixture; public sealed class UpperType { }");
        var lowerPath = Path.Combine(workspace.Path, "sample.dll");
        if (!OperatingSystem.IsWindows())
        {
            _ = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "sample",
                "namespace Fixture; public sealed class LowerType { }");
        }

        using var loader = new AssemblyLoader();
        var upperReader = asynchronous
            ? await loader.LoadFileAsync(upperPath)
            : loader.LoadFile(upperPath);
        var equivalentPath = Path.Combine(workspace.Path, ".", "Sample.dll");
        var equivalentReader = asynchronous
            ? await loader.LoadFileAsync(equivalentPath)
            : loader.LoadFile(equivalentPath);
        var lowerReader = asynchronous
            ? await loader.LoadFileAsync(lowerPath)
            : loader.LoadFile(lowerPath);

        Assert.Same(upperReader, equivalentReader);
        if (OperatingSystem.IsWindows())
        {
            Assert.Same(upperReader, lowerReader);
            Assert.Single(loader.LoadedAssemblies);
        }
        else
        {
            Assert.NotSame(upperReader, lowerReader);
            Assert.Equal(2, loader.LoadedAssemblies.Count);
            Assert.Equal("Sample", upperReader.GetString(upperReader.GetAssemblyDefinition().Name));
            Assert.Equal("sample", lowerReader.GetString(lowerReader.GetAssemblyDefinition().Name));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_CaseDistinctSearchDirectoriesRetainExactCandidate(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var firstDirectory = workspace.CreateDirectory("Refs");
        var secondDirectory = workspace.CreateDirectory("refs");
        var entryDirectory = workspace.CreateDirectory("entry");
        if (!OperatingSystem.IsWindows())
        {
            var wrongPath = TestAssemblyFiles.BuildAssemblyFile(firstDirectory, "WrongDependency",
                "namespace Fixture; public sealed class WrongDependency { }");
            File.Move(wrongPath, Path.Combine(firstDirectory, "Dependency.dll"));
        }

        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(secondDirectory, "Dependency",
            "namespace Fixture; public sealed class Dependency { }");
        var entryPath = TestAssemblyFiles.BuildAssemblyFile(entryDirectory, "Entry",
            "namespace Fixture; public sealed class Entry { public Dependency? Value => null; }",
            dependencyPath);

        using var loader = new AssemblyLoader(firstDirectory, secondDirectory + Path.DirectorySeparatorChar);
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(entryPath)
            : loader.LoadWithClosure(entryPath);
        using var set = result.Set;
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(result.EntryReader, "Fixture", "Dependency");

        Assert.True(set.TryResolveType(result.EntryReader, typeReference, out var dependencyReader, out _));
        Assert.Same(loader.LoadFile(dependencyPath), dependencyReader);
        Assert.Contains(dependencyReader, set.RegisteredAssemblies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_CaseDifferentConfiguredDirectoryDoesNotSuppressEntryDirectory(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var configuredDirectory = workspace.CreateDirectory("Entry");
        var entryDirectory = workspace.CreateDirectory("entry");
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(entryDirectory, "Dependency",
            "namespace Fixture; public sealed class Dependency { }");
        var entryPath = TestAssemblyFiles.BuildAssemblyFile(entryDirectory, "EntryAssembly",
            "namespace Fixture; public sealed class Entry { public Dependency? Value => null; }",
            dependencyPath);

        using var loader = new AssemblyLoader(configuredDirectory);
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(entryPath)
            : loader.LoadWithClosure(entryPath);
        using var set = result.Set;
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(result.EntryReader, "Fixture", "Dependency");

        Assert.True(set.TryResolveType(result.EntryReader, typeReference, out var dependencyReader, out _));
        Assert.Same(loader.LoadFile(dependencyPath), dependencyReader);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_CaseDistinctModuleFallbackDirectoryIsProbed(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var entryDirectory = workspace.CreateDirectory("Host");
        var fallbackDirectory = workspace.CreateDirectory("host");
        var modulePath = TestAssemblyFiles.BuildModuleFile(fallbackDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var entryPath = BuildAssemblyWithModules(entryDirectory, "Host",
            "namespace Fixture; public sealed class Host { public SharedType? Value => null; }",
            modulePath);

        using var loader = new AssemblyLoader(fallbackDirectory);
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(entryPath)
            : loader.LoadWithClosure(entryPath);
        using var set = result.Set;
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(result.EntryReader, "Fixture", "SharedType");

        Assert.True(set.TryResolveType(result.EntryReader, typeReference, out var moduleReader, out _));
        Assert.Same(loader.LoadFile(modulePath), moduleReader);
    }

    [Fact]
    public void LoadWithClosure_CaseDistinctModuleNamesFollowFileSystemIdentity()
    {
        using var workspace = new TemporaryDirectory();
        var upperModulePath = TestAssemblyFiles.BuildModuleFile(workspace.Path, "Shared",
            OperatingSystem.IsWindows()
                ? "namespace Fixture; public sealed class UpperType { } public sealed class LowerType { }"
                : "namespace Fixture; public sealed class UpperType { }");
        var lowerModulePath = Path.Combine(workspace.Path, "shared.netmodule");
        if (!OperatingSystem.IsWindows())
        {
            _ = TestAssemblyFiles.BuildModuleFile(workspace.Path, "shared",
                "namespace Fixture; public sealed class LowerType { }");
        }

        var entryPath = BuildAssemblyWithCaseDistinctModuleReferences(workspace.Path);

        using var loader = new AssemblyLoader();
        var (entryReader, set) = loader.LoadWithClosure(entryPath);
        using (set)
        {
            var upperReference = ResolutionTestHelpers.FindTypeReferenceHandle(entryReader, "Fixture", "UpperType");
            var lowerReference = ResolutionTestHelpers.FindTypeReferenceHandle(entryReader, "Fixture", "LowerType");
            Assert.Equal(HandleKind.ModuleReference, entryReader.GetTypeReference(upperReference).ResolutionScope.Kind);
            Assert.Equal(HandleKind.ModuleReference, entryReader.GetTypeReference(lowerReference).ResolutionScope.Kind);
            var upperModuleReference = (ModuleReferenceHandle)entryReader.GetTypeReference(upperReference).ResolutionScope;
            var lowerModuleReference = (ModuleReferenceHandle)entryReader.GetTypeReference(lowerReference).ResolutionScope;

            Assert.True(set.TryResolveType(entryReader, upperReference, out var upperReader, out var upperHandle));
            Assert.True(set.TryResolveType(entryReader, lowerReference, out var lowerReader, out var lowerHandle));
            Assert.Same(loader.LoadFile(upperModulePath), upperReader);
            Assert.Same(loader.LoadFile(lowerModulePath), lowerReader);
            if (OperatingSystem.IsWindows())
            {
                Assert.Same(upperReader, lowerReader);
            }
            else
            {
                Assert.NotSame(upperReader, lowerReader);
            }

            Assert.Equal("UpperType", upperReader.GetString(upperReader.GetTypeDefinition(upperHandle).Name));
            Assert.Equal("LowerType", lowerReader.GetString(lowerReader.GetTypeDefinition(lowerHandle).Name));
            Assert.True(loader.TryResolve(upperModuleReference, entryReader, out var upperDirectReader));
            Assert.True(loader.TryResolve(lowerModuleReference, entryReader, out var lowerDirectReader));
            Assert.Same(upperReader, upperDirectReader);
            Assert.Same(lowerReader, lowerDirectReader);
        }
    }

    private static string BuildAssemblyWithCaseDistinctModuleReferences(string directory)
    {
        var path = Path.Combine(directory, "Entry.dll");
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Entry.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("Entry"), new Version(1, 0, 0, 0), default, default,
            0, AssemblyHashAlgorithm.None);
        var upperModule = metadata.AddModuleReference(metadata.GetOrAddString("Shared.netmodule"));
        var lowerModule = metadata.AddModuleReference(metadata.GetOrAddString("shared.netmodule"));
        metadata.AddAssemblyFile(metadata.GetOrAddString("Shared.netmodule"), default, containsMetadata: true);
        metadata.AddAssemblyFile(metadata.GetOrAddString("shared.netmodule"), default, containsMetadata: true);
        metadata.AddTypeReference(upperModule, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("UpperType"));
        metadata.AddTypeReference(lowerModule, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("LowerType"));
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var image = new BlobBuilder();
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata),
            new BlobBuilder(), flags: CorFlags.ILOnly);
        pe.Serialize(image);
        File.WriteAllBytes(path, image.ToArray());
        return path;
    }

    private static string BuildAssemblyWithModules(string directory, string assemblyName, string source, params string[] modulePaths)
    {
        var builder = new TestAssemblyBuilder(assemblyName).WithSource(source);
        foreach (var modulePath in modulePaths)
        {
            builder.WithModuleReference(modulePath);
        }

        var path = Path.Combine(directory, assemblyName + ".dll");
        using var stream = builder.BuildPEStream();
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }
}

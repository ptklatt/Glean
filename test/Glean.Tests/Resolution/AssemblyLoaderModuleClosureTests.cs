using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyLoaderModuleClosureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_ModuleResolutionPrefersRequestingAssemblyDirectory(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var entryDirectory = workspace.CreateDirectory("entry");
        var fallbackDirectory = workspace.CreateDirectory("fallback");
        var localModulePath = TestAssemblyFiles.BuildModuleFile(entryDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class LocalMarker { }");
        _ = TestAssemblyFiles.BuildModuleFile(fallbackDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class FallbackMarker { }");
        var entryPath = BuildAssemblyWithModule(entryDirectory, "EntryAssembly", localModulePath,
            "namespace Fixture; public sealed class Entry { public SharedType? Value => null; }");

        using var loader = new AssemblyLoader(fallbackDirectory);
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(entryPath)
            : loader.LoadWithClosure(entryPath);
        using var set = result.Set;
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(result.EntryReader, "Fixture", "SharedType");
        Assert.Equal(HandleKind.ModuleReference, result.EntryReader.GetTypeReference(typeReference).ResolutionScope.Kind);

        Assert.True(set.TryResolveType(result.EntryReader, typeReference, out var moduleReader, out _));
        Assert.True(ContainsType(moduleReader, "LocalMarker"));
        Assert.False(ContainsType(moduleReader, "FallbackMarker"));
        Assert.Same(loader.LoadFile(localModulePath), moduleReader);

        set.Dispose();
        Assert.Same(loader.LoadFile(localModulePath), moduleReader);
        loader.Dispose();
        loader.Dispose();
        using var moduleFile = new FileStream(localModulePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_SharedDependencyReaderUsesEachClosureFallback(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var dependencyDirectory = workspace.CreateDirectory("dependency");
        var firstEntryDirectory = workspace.CreateDirectory("first");
        var secondEntryDirectory = workspace.CreateDirectory("second");
        var firstModulePath = TestAssemblyFiles.BuildModuleFile(firstEntryDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class FirstMarker { }");
        var secondModulePath = TestAssemblyFiles.BuildModuleFile(secondEntryDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class SecondMarker { }");
        var compiledDependencyPath = BuildAssemblyWithModule(firstEntryDirectory, "Dependency", firstModulePath,
            "namespace Fixture; public sealed class Dependency { public object Value => new SharedType(); }");
        var firstEntryPath = TestAssemblyFiles.BuildAssemblyFile(firstEntryDirectory, "FirstEntry",
            "namespace Fixture; public sealed class FirstEntry { public object Value => new Dependency().Value; }",
            compiledDependencyPath);
        var secondEntryPath = TestAssemblyFiles.BuildAssemblyFile(secondEntryDirectory, "SecondEntry",
            "namespace Fixture; public sealed class SecondEntry { public object Value => new Dependency().Value; }",
            compiledDependencyPath);
        var dependencyPath = Path.Combine(dependencyDirectory, "Dependency.dll");
        File.Move(compiledDependencyPath, dependencyPath);

        using var loader = new AssemblyLoader(dependencyDirectory);
        var first = asynchronous
            ? await loader.LoadWithClosureAsync(firstEntryPath)
            : loader.LoadWithClosure(firstEntryPath);
        using var firstSet = first.Set;
        loader.AddSearchPath(secondEntryDirectory);
        var second = asynchronous
            ? await loader.LoadWithClosureAsync(secondEntryPath)
            : loader.LoadWithClosure(secondEntryPath);
        using var secondSet = second.Set;
        var dependencyReader = loader.LoadFile(dependencyPath);
        Assert.Contains(dependencyReader, firstSet.RegisteredAssemblies);
        Assert.Contains(dependencyReader, secondSet.RegisteredAssemblies);
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(dependencyReader, "Fixture", "SharedType");
        Assert.Equal(HandleKind.ModuleReference, dependencyReader.GetTypeReference(typeReference).ResolutionScope.Kind);

        Assert.True(firstSet.TryResolveType(dependencyReader, typeReference, out var firstModuleReader, out _));
        Assert.True(secondSet.TryResolveType(dependencyReader, typeReference, out var secondModuleReader, out _));
        Assert.Same(loader.LoadFile(firstModulePath), firstModuleReader);
        Assert.Same(loader.LoadFile(secondModulePath), secondModuleReader);
        Assert.NotSame(firstModuleReader, secondModuleReader);
        Assert.True(ContainsType(firstModuleReader, "FirstMarker"));
        Assert.True(ContainsType(secondModuleReader, "SecondMarker"));
        Assert.True(firstSet.TryResolveType(dependencyReader, typeReference, out var firstAgain, out _));
        Assert.Same(firstModuleReader, firstAgain);
    }

    [Fact]
    public void LoadWithClosure_SameNamedModulesForDifferentAssembliesStaySeparate()
    {
        using var workspace = new TemporaryDirectory();
        var firstDirectory = workspace.CreateDirectory("first");
        var secondDirectory = workspace.CreateDirectory("second");
        var entryDirectory = workspace.CreateDirectory("entry");
        var firstModulePath = TestAssemblyFiles.BuildModuleFile(firstDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class FirstMarker { }");
        var secondModulePath = TestAssemblyFiles.BuildModuleFile(secondDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class SecondMarker { }");
        var firstAssemblyPath = BuildAssemblyWithModule(firstDirectory, "FirstAssembly", firstModulePath,
            "namespace Fixture; public sealed class FirstAssembly { public object Value => new SharedType(); }");
        var secondAssemblyPath = BuildAssemblyWithModule(secondDirectory, "SecondAssembly", secondModulePath,
            "namespace Fixture; public sealed class SecondAssembly { public object Value => new SharedType(); }");
        var entryPath = TestAssemblyFiles.BuildAssemblyFile(entryDirectory, "EntryAssembly",
            "namespace Fixture; public sealed class Entry { public object A => new FirstAssembly().Value; public object B => new SecondAssembly().Value; }",
            firstAssemblyPath, secondAssemblyPath);

        using var loader = new AssemblyLoader(firstDirectory, secondDirectory);
        var (_, set) = loader.LoadWithClosure(entryPath);
        using (set)
        {
            var firstAssemblyReader = loader.LoadFile(firstAssemblyPath);
            var secondAssemblyReader = loader.LoadFile(secondAssemblyPath);
            Assert.Contains(firstAssemblyReader, set.RegisteredAssemblies);
            Assert.Contains(secondAssemblyReader, set.RegisteredAssemblies);
            var firstTypeReference = ResolutionTestHelpers.FindTypeReferenceHandle(firstAssemblyReader, "Fixture", "SharedType");
            var secondTypeReference = ResolutionTestHelpers.FindTypeReferenceHandle(secondAssemblyReader, "Fixture", "SharedType");

            Assert.True(set.TryResolveType(firstAssemblyReader, firstTypeReference, out var firstModuleReader, out _));
            Assert.True(set.TryResolveType(secondAssemblyReader, secondTypeReference, out var secondModuleReader, out _));
            Assert.Same(loader.LoadFile(firstModulePath), firstModuleReader);
            Assert.Same(loader.LoadFile(secondModulePath), secondModuleReader);
            Assert.NotSame(firstModuleReader, secondModuleReader);
        }
    }

    [Fact]
    public void ManifestOnlyExport_ResolvesWithClosureAndDirectoryResolver()
    {
        using var workspace = new TemporaryDirectory();
        var manifestDirectory = workspace.CreateDirectory("manifest");
        var consumerDirectory = workspace.CreateDirectory("consumer");
        var modulePath = TestAssemblyFiles.BuildModuleFile(manifestDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var manifestPath = BuildAssemblyWithModule(manifestDirectory, "Manifest", modulePath,
            "namespace Fixture; public sealed class ManifestType { }");
        var consumerPath = TestAssemblyFiles.BuildAssemblyFile(consumerDirectory, "Consumer",
            "namespace Fixture; public sealed class Consumer { public SharedType? Value => null; }",
            manifestPath);

        using var closure = AssemblyClosure.Load(consumerPath, manifestDirectory);
        var manifestReader = closure.LoadedAssemblies[Path.GetFullPath(manifestPath)];
        Assert.Equal(0, manifestReader.GetTableRowCount(TableIndex.ModuleRef));
        Assert.Contains(manifestReader.ExportedTypes, handle =>
        {
            var exportedType = manifestReader.GetExportedType(handle);
            return (manifestReader.GetString(exportedType.Name) == "SharedType") &&
                   (exportedType.Implementation.Kind == HandleKind.AssemblyFile);
        });
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(closure.EntryReader, "Fixture", "SharedType");
        Assert.Equal(HandleKind.AssemblyReference, closure.EntryReader.GetTypeReference(typeReference).ResolutionScope.Kind);

        Assert.True(closure.Set.TryResolveType(closure.EntryReader, typeReference, out var moduleReader, out _));
        Assert.Same(closure.LoadedAssemblies[Path.GetFullPath(modulePath)], moduleReader);

        using var directoryResolver = new DirectoryAssemblyResolver(manifestDirectory);
        using var set = new AssemblySet(directoryResolver, directoryResolver);
        set.Add(closure.EntryReader);
        Assert.True(set.TryResolveType(closure.EntryReader, typeReference, out var directoryModuleReader, out _));
        Assert.True(ContainsType(directoryModuleReader, "SharedType"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManifestOnlyExport_SharedReaderUsesEachClosureFallback(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        var dependencyDirectory = workspace.CreateDirectory("dependency");
        var firstEntryDirectory = workspace.CreateDirectory("first");
        var secondEntryDirectory = workspace.CreateDirectory("second");
        var firstModulePath = TestAssemblyFiles.BuildModuleFile(firstEntryDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class FirstMarker { }");
        var secondModulePath = TestAssemblyFiles.BuildModuleFile(secondEntryDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { } public sealed class SecondMarker { }");
        var compiledManifestPath = BuildAssemblyWithModule(firstEntryDirectory, "Manifest", firstModulePath,
            "namespace Fixture; public sealed class ManifestType { }");
        var firstEntryPath = TestAssemblyFiles.BuildAssemblyFile(firstEntryDirectory, "FirstEntry",
            "namespace Fixture; public sealed class FirstEntry { public SharedType? Value => null; }",
            compiledManifestPath);
        var secondEntryPath = TestAssemblyFiles.BuildAssemblyFile(secondEntryDirectory, "SecondEntry",
            "namespace Fixture; public sealed class SecondEntry { public SharedType? Value => null; }",
            compiledManifestPath);
        var manifestPath = Path.Combine(dependencyDirectory, "Manifest.dll");
        File.Move(compiledManifestPath, manifestPath);

        using var loader = new AssemblyLoader(dependencyDirectory);
        var first = asynchronous
            ? await loader.LoadWithClosureAsync(firstEntryPath)
            : loader.LoadWithClosure(firstEntryPath);
        using var firstSet = first.Set;
        loader.AddSearchPath(secondEntryDirectory);
        var second = asynchronous
            ? await loader.LoadWithClosureAsync(secondEntryPath)
            : loader.LoadWithClosure(secondEntryPath);
        using var secondSet = second.Set;
        var manifestReader = loader.LoadFile(manifestPath);
        Assert.Contains(manifestReader, firstSet.RegisteredAssemblies);
        Assert.Contains(manifestReader, secondSet.RegisteredAssemblies);
        Assert.Equal(0, manifestReader.GetTableRowCount(TableIndex.ModuleRef));
        var firstTypeReference = ResolutionTestHelpers.FindTypeReferenceHandle(first.EntryReader, "Fixture", "SharedType");
        var secondTypeReference = ResolutionTestHelpers.FindTypeReferenceHandle(second.EntryReader, "Fixture", "SharedType");

        Assert.True(firstSet.TryResolveType(first.EntryReader, firstTypeReference, out var firstModuleReader, out _));
        Assert.True(secondSet.TryResolveType(second.EntryReader, secondTypeReference, out var secondModuleReader, out _));
        Assert.Same(loader.LoadFile(firstModulePath), firstModuleReader);
        Assert.Same(loader.LoadFile(secondModulePath), secondModuleReader);
        Assert.NotSame(firstModuleReader, secondModuleReader);
    }

    [Fact]
    public void AssemblySet_CustomModuleResolverStillHandlesModuleReferences()
    {
        using var workspace = new TemporaryDirectory();
        var modulePath = TestAssemblyFiles.BuildModuleFile(workspace.Path, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var assemblyPath = BuildAssemblyWithModule(workspace.Path, "Manifest", modulePath,
            "namespace Fixture; public sealed class Manifest { public SharedType? Value => null; }");
        using var loader = new AssemblyLoader();
        var assemblyReader = loader.LoadFile(assemblyPath);
        var moduleReader = loader.LoadFile(modulePath);
        var resolver = new FixedModuleResolver(moduleReader);
        using var set = new AssemblySet(resolver: null, moduleResolver: resolver);
        set.Add(assemblyReader);
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(assemblyReader, "Fixture", "SharedType");

        Assert.True(set.TryResolveType(assemblyReader, typeReference, out var resolvedReader, out _));
        Assert.Same(moduleReader, resolvedReader);
        Assert.Equal(1, resolver.Calls);
    }

    [Fact]
    public void AssemblyClosure_Load_ResolvesModuleAndReleasesItsFile()
    {
        using var workspace = new TemporaryDirectory();
        var entryDirectory = workspace.CreateDirectory("entry");
        var modulePath = TestAssemblyFiles.BuildModuleFile(entryDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var entryPath = BuildAssemblyWithModule(entryDirectory, "EntryAssembly", modulePath,
            "namespace Fixture; public sealed class Entry { public SharedType? Value => null; }");

        using var closure = AssemblyClosure.Load(entryPath);
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(closure.EntryReader, "Fixture", "SharedType");
        Assert.True(closure.Set.TryResolveType(closure.EntryReader, typeReference, out var moduleReader, out _));
        Assert.Same(closure.LoadedAssemblies[Path.GetFullPath(modulePath)], moduleReader);

        closure.Dispose();
        closure.Dispose();
        using var moduleFile = new FileStream(modulePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void LoadWithClosure_InvalidLocalModuleFallsBackAndReleasesFailedCandidate()
    {
        using var workspace = new TemporaryDirectory();
        var entryDirectory = workspace.CreateDirectory("entry");
        var fallbackDirectory = workspace.CreateDirectory("fallback");
        var fallbackModulePath = TestAssemblyFiles.BuildModuleFile(fallbackDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var entryPath = BuildAssemblyWithModule(entryDirectory, "EntryAssembly", fallbackModulePath,
            "namespace Fixture; public sealed class Entry { public SharedType? Value => null; }");
        var invalidModulePath = Path.Combine(entryDirectory, "Shared.netmodule");
        File.WriteAllText(invalidModulePath, "invalid module image");

        using var loader = new AssemblyLoader(fallbackDirectory);
        var (entryReader, set) = loader.LoadWithClosure(entryPath);
        using (set)
        {
            var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(entryReader, "Fixture", "SharedType");
            Assert.True(set.TryResolveType(entryReader, typeReference, out var moduleReader, out _));
            Assert.Same(loader.LoadFile(fallbackModulePath), moduleReader);
            using var invalidFile = new FileStream(invalidModulePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        loader.Dispose();
        using var fallbackFile = new FileStream(fallbackModulePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static string BuildAssemblyWithModule(string directory, string assemblyName, string modulePath, string source)
    {
        var path = Path.Combine(directory, assemblyName + ".dll");
        using var stream = new TestAssemblyBuilder(assemblyName)
            .WithModuleReference(modulePath)
            .WithSource(source)
            .BuildPEStream();
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    private static bool ContainsType(MetadataReader reader, string name)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            if (reader.GetString(reader.GetTypeDefinition(handle).Name) == name)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FixedModuleResolver(MetadataReader reader) : IModuleResolver
    {
        public int Calls { get; private set; }

        public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            Calls++;
            resolvedReader = reader;
            return true;
        }
    }
}

using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyLoaderOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoadWithClosure_ReusedReaders_StillVisitsTransitiveDependencies(bool asynchronous, bool preloadFile)
    {
        using var fixture = new ChainFixture();
        using var loader = new AssemblyLoader(fixture.Directory, fixture.RuntimeDirectory);
        if (preloadFile)
        {
            _ = loader.LoadFile(fixture.DependencyPath);
        }
        else
        {
            var first = loader.LoadWithClosure(fixture.EntryPath);
            first.Set.Dispose();
        }

        var failures = new List<AssemblyDependencyLoadFailure>();
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Empty(failures);
        Assert.Contains(loader.LoadFile(fixture.DependencyPath), set.RegisteredAssemblies);
        Assert.Contains(loader.LoadFile(fixture.LeafPath), set.RegisteredAssemblies);
        Assert.Same(result.EntryReader, loader.LoadFile(fixture.EntryPath));
    }

    [Fact]
    public async Task TryResolve_SharedLoader_ReusesReadersAfterAssemblySetIsDisposed()
    {
        using var fixture = new ChainFixture();
        using var loader = new AssemblyLoader(fixture.Directory);
        var entry = loader.LoadFile(fixture.EntryPath);
        var reference = ResolutionTestHelpers.FindAssemblyReferenceHandle(entry, "DependencyLibrary");
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(entry, "Fixture", "Dependency");
        using (var set = new AssemblySet(loader, loader))
        {
            set.Add(entry);
            Assert.True(set.TryResolveType(entry, typeReference, out var resolved, out _));
            Assert.Same(loader.LoadFile(fixture.DependencyPath), resolved);
        }

        Assert.True(loader.TryResolve(reference, entry, out var reader));
        Assert.Same(loader.LoadFile(fixture.DependencyPath), reader);
        Assert.Same(await loader.LoadFileAsync(fixture.DependencyPath), reader);
        Assert.Equal(2, loader.LoadedAssemblies.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadWithClosure_TargetFramework_UsesTheSameNuGetProbesAsResolution(bool asynchronous)
    {
        using var fixture = new ChainFixture();
        var packageDirectory = Path.Combine(fixture.Directory, "package");
        var libraryDirectory = Path.Combine(packageDirectory, "lib", "net9.0");
        Directory.CreateDirectory(libraryDirectory);
        var dependencyPath = Path.Combine(libraryDirectory, "DependencyLibrary.dll");
        File.Move(fixture.DependencyPath, dependencyPath);
        using var loader = new AssemblyLoader("net9.0", [packageDirectory, fixture.Directory, fixture.RuntimeDirectory]);

        var failures = new List<AssemblyDependencyLoadFailure>();
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;
        var reference = ResolutionTestHelpers.FindAssemblyReferenceHandle(result.EntryReader, "DependencyLibrary");

        Assert.Empty(failures);
        Assert.True(loader.TryResolve(reference, result.EntryReader, out var resolved));
        Assert.Same(loader.LoadFile(dependencyPath), resolved);
        Assert.Contains(resolved!, set.RegisteredAssemblies);
        Assert.Contains(loader.LoadFile(fixture.LeafPath), set.RegisteredAssemblies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadFile_CachedReader_StillHonorsCancellation(bool asynchronous)
    {
        using var fixture = new ChainFixture();
        using var loader = new AssemblyLoader();
        _ = loader.LoadFile(fixture.EntryPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        if (asynchronous)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loader.LoadFileAsync(fixture.EntryPath, cancellation.Token));
        }
        else
        {
            Assert.ThrowsAny<OperationCanceledException>(() => loader.LoadFile(fixture.EntryPath, cancellation.Token));
        }
    }

    [Fact]
    public async Task Dispose_RepeatedCalls_ReleaseFilesAndRejectFurtherOperations()
    {
        using var fixture = new ChainFixture();
        using var loader = new AssemblyLoader(fixture.Directory);
        var entry = loader.LoadFile(fixture.EntryPath);
        var reference = ResolutionTestHelpers.FindAssemblyReferenceHandle(entry, "DependencyLibrary");
        Assert.True(loader.TryResolve(reference, entry, out _));

        loader.Dispose();
        loader.Dispose();

        Assert.Empty(loader.LoadedPaths);
        Assert.Empty(loader.LoadedAssemblies);
        Assert.Throws<ObjectDisposedException>(() => loader.LoadFile(fixture.EntryPath));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => loader.LoadFileAsync(fixture.EntryPath));
        Assert.Throws<ObjectDisposedException>(() => loader.AddSearchPath(fixture.Directory));
        Assert.Throws<ObjectDisposedException>(() => loader.TryResolve(reference, entry, out _));
        Assert.Throws<ObjectDisposedException>(() => loader.TryResolve(MetadataTokens.ModuleReferenceHandle(1), entry, out _));
        using var entryFile = new FileStream(fixture.EntryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var dependencyFile = new FileStream(fixture.DependencyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void DirectoryResolver_DisposedTwice_ReleasesItsReaders()
    {
        using var fixture = new ChainFixture();
        using var entryLoader = new AssemblyLoader();
        var entry = entryLoader.LoadFile(fixture.EntryPath);
        var reference = ResolutionTestHelpers.FindAssemblyReferenceHandle(entry, "DependencyLibrary");
        using var resolver = new DirectoryAssemblyResolver(fixture.Directory);
        Assert.True(resolver.TryResolve(reference, entry, out _));

        resolver.Dispose();
        resolver.Dispose();

        Assert.Throws<ObjectDisposedException>(() => resolver.TryResolve(reference, entry, out _));
        Assert.Throws<ObjectDisposedException>(() => resolver.AddSearchPath(fixture.Directory));
        using var dependencyFile = new FileStream(fixture.DependencyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Closure_DisposedTwice_ReleasesItsLoader()
    {
        using var fixture = new ChainFixture();
        using var closure = AssemblyClosure.Load(fixture.EntryPath, fixture.Directory, fixture.RuntimeDirectory);

        closure.Dispose();
        closure.Dispose();

        Assert.Throws<ObjectDisposedException>(() => closure.EntryReader);
        using var entryFile = new FileStream(fixture.EntryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var dependencyFile = new FileStream(fixture.DependencyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void LoadFile_InvalidMetadata_ReleasesInputFile()
    {
        using var fixture = new ChainFixture();
        var bytes = File.ReadAllBytes(fixture.EntryPath);
        var metadataOffset = bytes.AsSpan().IndexOf("BSJB"u8);
        Assert.True(metadataOffset >= 0);
        bytes[metadataOffset] = 0;
        File.WriteAllBytes(fixture.EntryPath, bytes);
        using var loader = new AssemblyLoader();

        Assert.Throws<BadImageFormatException>(() => loader.LoadFile(fixture.EntryPath));

        Assert.Empty(loader.LoadedAssemblies);
        using var entryFile = new FileStream(fixture.EntryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private sealed class ChainFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace = new();

        public ChainFixture()
        {
            LeafPath = TestAssemblyFiles.BuildAssemblyFile(_workspace.Path, "LeafLibrary",
                "namespace Fixture; public sealed class Leaf { }");
            DependencyPath = TestAssemblyFiles.BuildAssemblyFile(_workspace.Path, "DependencyLibrary",
                "namespace Fixture; public sealed class Dependency { public Leaf Value => new(); }", LeafPath);
            EntryPath = TestAssemblyFiles.BuildAssemblyFile(_workspace.Path, "EntryAssembly",
                "namespace Fixture; public sealed class Entry { public Dependency Value => new(); }", DependencyPath, LeafPath);
        }

        public string Directory => _workspace.Path;
        public string RuntimeDirectory => Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        public string EntryPath { get; }
        public string DependencyPath { get; }
        public string LeafPath { get; }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }
}

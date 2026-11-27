using System.Reflection.Metadata;

using Xunit;

using Glean.Contexts;
using Glean.Extensions;
using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class PeReaderAccessTests
{
    private const string Source = "namespace Fixture; public static class Value { public static int Read() => 42; }";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Loader_ReturnsSameOwnedImageForAssembliesAndModules(bool asynchronous, bool module)
    {
        using var workspace = new TemporaryDirectory();
        string path = module
            ? TestAssemblyFiles.BuildModuleFile(workspace.Path, "Value", Source)
            : TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Value", Source);
        using var loader = new AssemblyLoader();
        var reader = asynchronous ? await loader.LoadFileAsync(path) : loader.LoadFile(path);

        Assert.True(loader.TryGetPeReader(reader, out var image));
        Assert.True(loader.TryGetPeReader(reader, out var repeated));
        Assert.Same(image, repeated);
        Assert.Same(reader, asynchronous ? await loader.LoadFileAsync(path) : loader.LoadFile(path));
        var definition = reader.GetMethodDefinition(Assert.Single(reader.MethodDefinitions));
        var expected = image.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes();
        Assert.Equal(expected, definition.GetILSpan(image).ToArray());
        var imageReader = image.GetMetadataReader();
        Assert.Equal(reader.GetGuid(reader.GetModuleDefinition().Mvid), imageReader.GetGuid(imageReader.GetModuleDefinition().Mvid));
    }

    [Fact]
    public async Task Loader_AsyncImageRemainsAvailableAfterSourceFileIsDeleted()
    {
        using var workspace = new TemporaryDirectory();
        string path = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Value", Source);
        using var loader = new AssemblyLoader();
        var reader = await loader.LoadFileAsync(path);
        File.Delete(path);

        Assert.True(loader.TryGetPeReader(reader, out var image));
        var method = reader.GetMethodDefinition(Assert.Single(reader.MethodDefinitions));
        Assert.False(method.GetILSpan(image).IsEmpty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Loader_MetadataFromAnotherOpenOfSameFile_IsNotOwned(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string path = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Value", Source);
        using var foreign = AssemblyScope.Open(path);
        using var loader = new AssemblyLoader();
        var reader = asynchronous ? await loader.LoadFileAsync(path) : loader.LoadFile(path);

        Assert.True(loader.TryGetPeReader(reader, out _));
        Assert.False(loader.TryGetPeReader(foreign.Reader, out var image));
        Assert.Null(image);
        Assert.Equal("reader", Assert.Throws<ArgumentNullException>(() => loader.TryGetPeReader(null!, out _)).ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Loader_DisposeReleasesBorrowedImageAndRejectsLookups(bool asynchronous)
    {
        using var workspace = new TemporaryDirectory();
        string path = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Value", Source);
        using var loader = new AssemblyLoader();
        var reader = asynchronous ? await loader.LoadFileAsync(path) : loader.LoadFile(path);
        Assert.True(loader.TryGetPeReader(reader, out var image));
        int rva = reader.GetMethodDefinition(Assert.Single(reader.MethodDefinitions)).RelativeVirtualAddress;

        loader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => loader.TryGetPeReader(reader, out _));
        Assert.Throws<ObjectDisposedException>(() => image.GetSectionData(rva));
        loader.Dispose();
    }

    [Fact]
    public void Closure_ReturnsEntryAndDependencyImagesForItsReaders()
    {
        using var workspace = new TemporaryDirectory();
        string dependency = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Dependency", Source);
        string entry = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Entry",
            "namespace Fixture; public static class Entry { public static int Run() => Value.Read(); }", dependency);
        using var closure = AssemblyClosure.Load(entry, workspace.Path, Path.GetDirectoryName(typeof(object).Assembly.Location)!).ThrowIfPartial();

        Assert.True(closure.TryGetPeReader(closure.EntryReader, out var entryImage));
        Assert.Same(entryImage, closure.EntryPeReader);
        foreach (var reader in closure.LoadedAssemblies.Values)
        {
            Assert.True(closure.TryGetPeReader(reader, out var image));
            Assert.True(closure.TryGetPeReader(reader, out var repeated));
            Assert.Same(image, repeated);
            var imageReader = image.GetMetadataReader();
            Assert.Equal(reader.GetGuid(reader.GetModuleDefinition().Mvid), imageReader.GetGuid(imageReader.GetModuleDefinition().Mvid));
        }

        var reference = ResolutionTestHelpers.FindTypeReferenceHandle(closure.EntryReader, "Fixture", "Value");
        Assert.True(closure.Set.TryResolveType(closure.EntryReader, reference, out var dependencyReader, out var handle));
        Assert.True(closure.TryGetPeReader(dependencyReader, out var dependencyImage));
        Assert.NotSame(entryImage, dependencyImage);
        var method = MethodContext.Create(dependencyReader, Assert.Single(dependencyReader.GetTypeDefinition(handle).GetMethods()));
        Assert.False(method.Definition.GetILSpan(dependencyImage).IsEmpty);
    }

    [Fact]
    public void Closure_ExternallyRegisteredMetadataRemainsUnowned()
    {
        using var workspace = new TemporaryDirectory();
        string entry = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Entry", Source);
        string external = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "External", Source);
        using var foreign = AssemblyScope.Open(external);
        using var closure = AssemblyClosure.Load(entry);
        closure.Set.Add(foreign.Reader);

        Assert.False(closure.TryGetPeReader(foreign.Reader, out var image));
        Assert.Null(image);
        Assert.Equal("reader", Assert.Throws<ArgumentNullException>(() => closure.TryGetPeReader(null!, out _)).ParamName);
        var owned = closure.EntryPeReader;
        var reader = closure.EntryReader;
        int rva = reader.GetMethodDefinition(Assert.Single(reader.MethodDefinitions)).RelativeVirtualAddress;
        closure.Dispose();

        Assert.Throws<ObjectDisposedException>(() => closure.EntryPeReader);
        Assert.Throws<ObjectDisposedException>(() => closure.TryGetPeReader(reader, out _));
        Assert.Throws<ObjectDisposedException>(() => closure.TryGetManagedEntryPoint(out _));
        Assert.Throws<ObjectDisposedException>(() => owned.GetSectionData(rva));
        Assert.True(foreign.PeReader.HasMetadata);
    }

    [Fact]
    public void DirectoryResolver_ReturnsOnlyItsOwnBorrowedImages()
    {
        using var workspace = new TemporaryDirectory();
        string dependency = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Dependency", Source);
        string entry = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Entry",
            "namespace Fixture; public static class Entry { public static int Run() => Value.Read(); }", dependency);
        using var scope = AssemblyScope.Open(entry);
        using var resolver = new DirectoryAssemblyResolver(workspace.Path);
        var reference = scope.Reader.AssemblyReferences.Single(h => scope.Reader.GetString(scope.Reader.GetAssemblyReference(h).Name) == "Dependency");

        Assert.True(resolver.TryResolve(reference, scope.Reader, out var reader));
        Assert.True(resolver.TryGetPeReader(reader!, out var image));
        Assert.False(resolver.TryGetPeReader(scope.Reader, out var foreign));
        Assert.Null(foreign);
        Assert.Equal("reader", Assert.Throws<ArgumentNullException>(() => resolver.TryGetPeReader(null!, out _)).ParamName);
        int rva = reader!.GetMethodDefinition(Assert.Single(reader.MethodDefinitions)).RelativeVirtualAddress;
        Assert.False(reader.GetMethodDefinition(Assert.Single(reader.MethodDefinitions)).GetILSpan(image).IsEmpty);
        resolver.Dispose();

        Assert.Throws<ObjectDisposedException>(() => resolver.TryGetPeReader(reader, out _));
        Assert.Throws<ObjectDisposedException>(() => image.GetSectionData(rva));
        Assert.True(scope.PeReader.HasMetadata);
    }
}

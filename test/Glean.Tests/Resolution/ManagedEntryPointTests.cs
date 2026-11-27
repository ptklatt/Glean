using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis;
using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class ManagedEntryPointTests
{
    [Fact]
    public void Scope_ConsoleEntryPointRetainsItsReaderAndProvidesIL()
    {
        using var workspace = new TemporaryDirectory();
        string path = WriteConsole(workspace);
        using var scope = AssemblyScope.Open(path);

        Assert.True(scope.TryGetManagedEntryPoint(out var method));
        Assert.Same(scope.Reader, method.Reader);
        Assert.Equal("Main", method.Name);
        Assert.Equal(scope.PeReader.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress, MetadataTokens.GetToken(method.Handle));
        Assert.False(method.Definition.GetILSpan(scope.PeReader).IsEmpty);
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.TryGetManagedEntryPoint(out _));
    }

    [Fact]
    public void Closure_EntryPointWorksWithoutACompleteDependencyClosure()
    {
        using var workspace = new TemporaryDirectory();
        string path = WriteConsole(workspace);
        using var closure = AssemblyClosure.Load(path);

        Assert.NotEmpty(closure.SkippedDependencies);
        Assert.True(closure.TryGetManagedEntryPoint(out var method));
        Assert.Same(closure.EntryReader, method.Reader);
        Assert.Equal("Main", method.Name);
        Assert.True(closure.TryGetPeReader(method.Reader, out var image));
        Assert.Same(closure.EntryPeReader, image);
        Assert.False(method.Definition.GetILSpan(image).IsEmpty);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0x06000000, false)]
    [InlineData(0x06000002, false)]
    [InlineData(0x02000001, false)]
    [InlineData(0x26000001, false)]
    [InlineData(0x70000001, false)]
    [InlineData(unchecked((int)0xff000001), false)]
    [InlineData(unchecked((int)0x86000001), false)]
    [InlineData(0x06000001, true)]
    public void Scope_NonLocalOrInvalidEntryPoint_ReturnsFalse(int token, bool native)
    {
        using var workspace = new TemporaryDirectory();
        string path = WriteImage(workspace, token, native);
        using var scope = AssemblyScope.Open(path);

        Assert.Equal(token, scope.PeReader.PEHeaders.CorHeader!.EntryPointTokenOrRelativeVirtualAddress);
        Assert.False(scope.TryGetManagedEntryPoint(out var method));
        Assert.False(method.IsValid);
    }

    private static string WriteConsole(TemporaryDirectory workspace)
    {
        string path = Path.Combine(workspace.Path, "Entry.exe");
        using var stream = new TestAssemblyBuilder("Entry")
            .WithOutputKind(OutputKind.ConsoleApplication)
            .WithSource("public static class Program { public static int Main() => 42; }")
            .BuildPEStream();
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    private static string WriteImage(TemporaryDirectory workspace, int token, bool native)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Entry.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("Entry"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        metadata.AddAssemblyFile(metadata.GetOrAddString("Entry.netmodule"), default, containsMetadata: true);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Program"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
            metadata.GetOrAddString("Main"), metadata.GetOrAddBlob(new byte[] { 0, 0, 1 }), -1, MetadataTokens.ParameterHandle(1));
        var image = new BlobBuilder();
        new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(),
            entryPoint: MetadataTokens.MethodDefinitionHandle(1), flags: native ? CorFlags.NativeEntryPoint : CorFlags.ILOnly).Serialize(image);
        byte[] bytes = image.ToArray();
        using (var pe = new PEReader(new MemoryStream(bytes, writable: false)))
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(pe.PEHeaders.CorHeaderStartOffset + 20, 4), token);
        }

        string path = Path.Combine(workspace.Path, "Entry.dll");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

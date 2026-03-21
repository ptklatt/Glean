using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Enumerators;

namespace Glean.Tests.Enumerators;

public class AssemblyReferenceEnumeratorTests
{
    [Fact]
    public void Enumerate_SyntheticAssemblyReferences_ReturnsContextsInMetadataOrder()
    {
        using var provider = CreateAssemblyReferenceMetadataReaderProvider();
        var reader = provider.GetMetadataReader();
        var enumerator = AssemblyReferenceEnumerator.Create(reader, reader.AssemblyReferences);

        Assert.True(enumerator.MoveNext());
        var first = enumerator.Current;
        Assert.Equal("System.Runtime", first.Name);
        Assert.Equal(new Version(8, 0, 0, 0), first.Version);
        Assert.Equal(string.Empty, first.Culture);

        Assert.True(enumerator.MoveNext());
        var second = enumerator.Current;
        Assert.Equal("mscorlib", second.Name);
        Assert.Equal(new Version(4, 0, 0, 0), second.Version);
        Assert.Equal("neutral", second.Culture);

        Assert.False(enumerator.MoveNext());
    }

    private static MetadataReaderProvider CreateAssemblyReferenceMetadataReaderProvider()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("Synthetic.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("Synthetic"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            0);
        metadata.AddAssemblyReference(
            metadata.GetOrAddString("System.Runtime"),
            new Version(8, 0, 0, 0),
            default,
            default,
            0,
            default);
        metadata.AddAssemblyReference(
            metadata.GetOrAddString("mscorlib"),
            new Version(4, 0, 0, 0),
            metadata.GetOrAddString("neutral"),
            default,
            0,
            default);
        metadata.AddTypeDefinition(
            0,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var root = new MetadataRootBuilder(metadata);
        var image = new BlobBuilder();
        root.Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }
}

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;

namespace Glean.Tests.Extensions;

public class AssemblyReferenceExtensionsTests
{
    // == Identity checks =====================================================

    [Fact]
    public void IdentityChecks_SyntheticAssemblyReference_ReportExpectedValues()
    {
        using var provider = CreateAssemblyReferenceMetadataReaderProvider();
        var reader = provider.GetMetadataReader();
        var assemblyReference = reader.GetAssemblyReference(GetAssemblyReferenceHandle(reader, "System.Runtime"));

        Assert.True(assemblyReference.NameIs(reader, "System.Runtime"));
        Assert.False(assemblyReference.NameIs(reader, "mscorlib"));

        Assert.True(assemblyReference.Is(reader, "System.Runtime"));
        Assert.True(assemblyReference.Is(reader, "System.Runtime", new Version(8, 0, 0, 0)));
        Assert.False(assemblyReference.Is(reader, "System.Runtime", new Version(9, 0, 0, 0)));
        Assert.False(assemblyReference.Is(reader, "mscorlib"));
    }

    private static AssemblyReferenceHandle GetAssemblyReferenceHandle(MetadataReader reader, string assemblyName)
    {
        foreach (var handle in reader.AssemblyReferences)
        {
            var assemblyReference = reader.GetAssemblyReference(handle);
            if (assemblyReference.NameIs(reader, assemblyName))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not locate assembly reference {assemblyName}.");
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
            default,
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

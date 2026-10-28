using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;

namespace Glean.Tests.Resolution;

public sealed class ResolutionLookupConstructionTests
{
    [Fact]
    public void TypeLookup_ExcludesNestedFlagsAndKeepsLastOrdinalDuplicate()
    {
        var metadata = Create();
        AddType(metadata, "Target", TypeAttributes.Public);
        var lower = AddType(metadata, "target", TypeAttributes.Public);
        AddType(metadata, "Nested", TypeAttributes.NestedPublic);
        var latest = AddType(metadata, "Target", TypeAttributes.Public);
        using var provider = Serialize(metadata);
        var lookup = ResolutionCore.BuildTypeLookup(provider.GetMetadataReader());
        Assert.Equal(2, lookup.Count);
        Assert.Equal(latest, lookup[("Fixture", "Target")]);
        Assert.Equal(lower, lookup[("Fixture", "target")]);
        Assert.False(lookup.ContainsKey(("Fixture", "Nested")));
    }

    [Fact]
    public void ForwarderLookup_KeepsLastOrdinalDuplicate()
    {
        var metadata = Create();
        var assembly = metadata.AddAssemblyReference(metadata.GetOrAddString("Dependency"), new Version(1, 0, 0, 0), default, default, 0, default);
        metadata.AddExportedType(TypeAttributes.Public, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Target"), assembly, 0);
        var lower = metadata.AddExportedType(TypeAttributes.Public, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("target"), assembly, 0);
        var latest = metadata.AddExportedType(TypeAttributes.Public, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Target"), assembly, 0);
        using var provider = Serialize(metadata);
        var lookup = ResolutionCore.BuildForwarderLookup(provider.GetMetadataReader());
        Assert.Equal(2, lookup.Count);
        Assert.Equal(latest, lookup[("Fixture", "Target")]);
        Assert.Equal(lower, lookup[("Fixture", "target")]);
    }

    [Fact]
    public void EmptyTables_ProduceEmptyLookups()
    {
        using var provider = Serialize(Create());
        var reader = provider.GetMetadataReader();
        Assert.Empty(ResolutionCore.BuildTypeLookup(reader));
        Assert.Empty(ResolutionCore.BuildForwarderLookup(reader));
    }

    private static MetadataBuilder Create()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("lookups"), default, default, default);
        return metadata;
    }

    private static TypeDefinitionHandle AddType(MetadataBuilder metadata, string name, TypeAttributes flags)
        => metadata.AddTypeDefinition(flags, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString(name), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
    }
}

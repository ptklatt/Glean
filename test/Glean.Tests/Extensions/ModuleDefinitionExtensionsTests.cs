using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;

namespace Glean.Tests.Extensions;

public class ModuleDefinitionExtensionsTests
{
    // == Metadata access =====================================================

    [Fact]
    public void MetadataAccess_SyntheticManifestModule_ReturnExpectedValues()
    {
        Guid mvid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid generationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid baseGenerationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        using var provider = CreateModuleMetadataReaderProvider(
            "Synthetic.Manifest.dll",
            mvid,
            generationId,
            baseGenerationId,
            includeAssembly: true);
        var reader = provider.GetMetadataReader();
        var module = reader.GetModuleDefinition();

        Assert.Equal("Synthetic.Manifest.dll", module.GetName(reader));
        Assert.True(module.NameIs(reader, "Synthetic.Manifest.dll"));
        Assert.False(module.NameIs(reader, "Other.dll"));
        Assert.Equal(mvid, module.GetMvid(reader));
        Assert.Equal(generationId, module.GetGenerationId(reader));
        Assert.Equal(baseGenerationId, module.GetBaseGenerationId(reader));
        Assert.True(module.IsManifestModule(reader));
    }

    [Fact]
    public void IsManifestModule_ModuleWithoutAssemblyDefinition_ReturnsFalse()
    {
        using var provider = CreateModuleMetadataReaderProvider(
            "Synthetic.netmodule",
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            includeAssembly: false);
        var reader = provider.GetMetadataReader();
        var module = reader.GetModuleDefinition();

        Assert.False(module.IsManifestModule(reader));
    }

    private static MetadataReaderProvider CreateModuleMetadataReaderProvider(
        string moduleName,
        Guid mvid,
        Guid generationId,
        Guid baseGenerationId,
        bool includeAssembly)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString(moduleName),
            metadata.GetOrAddGuid(mvid),
            metadata.GetOrAddGuid(generationId),
            metadata.GetOrAddGuid(baseGenerationId));

        if (includeAssembly)
        {
            metadata.AddAssembly(
                metadata.GetOrAddString("Synthetic"),
                new Version(1, 0, 0, 0),
                default,
                default,
                0,
                0);
        }

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

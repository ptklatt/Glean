using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Tests.Utility;

namespace Glean.Tests.Contexts;

public sealed class ModuleContextTests
{
    private const string ModuleContextSource = """
        using System;

        [module: Glean.Tests.Contexts.ModuleMarker]

        namespace Glean.Tests.Contexts;

        [AttributeUsage(AttributeTargets.Module)]
        internal sealed class ModuleMarkerAttribute : Attribute
        {
        }

        internal sealed class ModuleContextFixture
        {
            public static void TouchConsole()
            {
                Console.WriteLine(string.Empty);
            }
        }
        """;

    [Fact]
    public void MetadataAccess_CompiledAssembly_ReturnsExpectedValues()
    {
        using var metadata = CreateModuleContextMetadataScope();
        var assembly = AssemblyContext.Create(metadata.Reader);
        var module = assembly.Module;

        Assert.True(module.IsValid);
        Assert.True(module.IsManifestModule);
        Assert.EndsWith(".dll", module.Name);
        Assert.NotEqual(Guid.Empty, module.Mvid);
        Assert.Equal(0, module.Generation);
        Assert.Null(module.GenerationId);
        Assert.Null(module.BaseGenerationId);

        Assert.True(module.TryFindType("Glean.Tests.Contexts", "ModuleContextFixture", out var type));
        Assert.True(type.IsValid);
        Assert.Equal("ModuleContextFixture", type.Name);

        Assert.True(module.HasAttribute("Glean.Tests.Contexts", "ModuleMarkerAttribute"));
        Assert.True(module.TryFindAttribute("Glean.Tests.Contexts", "ModuleMarkerAttribute", out var attribute));
        Assert.True(attribute.IsValid);

        bool foundConsoleReference = false;
        foreach (var typeReference in module.EnumerateTypeReferences())
        {
            if (typeReference.Is("System", "Console"))
            {
                foundConsoleReference = true;
                break;
            }
        }

        Assert.True(foundConsoleReference);

        bool foundWriteLineReference = false;
        foreach (var memberReference in module.EnumerateMemberReferences())
        {
            if (memberReference.NameIs("WriteLine"))
            {
                foundWriteLineReference = true;
                break;
            }
        }

        Assert.True(foundWriteLineReference);

        int assemblyReferenceCount = 0;
        foreach (var ignored in module.EnumerateAssemblyReferences())
        {
            assemblyReferenceCount++;
        }

        Assert.True(assemblyReferenceCount > 0);
    }

    [Fact]
    public void Create_ModuleOnlyMetadata_ReturnsNonManifestModule()
    {
        using var provider = CreateModuleOnlyMetadataReaderProvider();
        var module = ModuleContext.Create(provider.GetMetadataReader());

        Assert.True(module.IsValid);
        Assert.False(module.IsManifestModule);
        Assert.Equal("Synthetic.netmodule", module.Name);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), module.Mvid);
        Assert.True(module.TryFindType(string.Empty, "<Module>", out var globalType));
        Assert.True(globalType.IsValid);
    }

    private static MetadataScope CreateModuleContextMetadataScope()
    {
        return new TestAssemblyBuilder("ModuleContextFixture")
            .WithSource(ModuleContextSource)
            .BuildMetadataScope();
    }

    private static MetadataReaderProvider CreateModuleOnlyMetadataReaderProvider()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("Synthetic.netmodule"),
            metadata.GetOrAddGuid(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            default,
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

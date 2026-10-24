using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Extensions;

namespace Glean.Tests.Extensions;

public sealed class ExportedTypeExtensionsTests
{
    private const TypeAttributes ForwarderTypeAttribute = (TypeAttributes)0x00200000;

    [Fact]
    public void TryGetForwardedResolutionScope_FollowsNestedForwardersToAssemblyReference()
    {
        using var provider = CreateExportedTypeMetadataReaderProvider();
        var assembly = AssemblyContext.Create(provider.GetMetadataReader());
        var exportedTypes = assembly.EnumerateExportedTypes();

        Assert.True(exportedTypes.MoveNext());
        var forwarded = exportedTypes.Current;
        Assert.True(forwarded.TryGetForwardedResolutionScope(out var forwardedScope));
        Assert.Equal(HandleKind.AssemblyReference, forwardedScope.Kind);

        Assert.True(exportedTypes.MoveNext());
        var nested = exportedTypes.Current;
        Assert.True(nested.TryGetForwardedResolutionScope(out var nestedScope));
        Assert.Equal(HandleKind.AssemblyReference, nestedScope.Kind);
    }

    [Fact]
    public void TryGetForwardedResolutionScope_StopsWhenChainExceedsDepthLimit()
    {
        using var provider = CreateDeepExportedTypeMetadataReaderProvider(17, out var startHandle);
        var reader = provider.GetMetadataReader();
        var exportedType = reader.GetExportedType(startHandle);

        Assert.False(exportedType.TryGetForwardedResolutionScope(reader, out var finalScope));
        Assert.True(finalScope.IsNil);
    }

    private static MetadataReaderProvider CreateExportedTypeMetadataReaderProvider()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticExportedTypes.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("SyntheticExportedTypes"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);

        var assemblyReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("Forwarded.Library"),
            new Version(2, 0, 0, 0),
            default,
            default,
            0,
            default);
        var forwardedType = metadata.AddExportedType(
            TypeAttributes.Public | ForwarderTypeAttribute,
            metadata.GetOrAddString("Glean.Tests.Forwarded"),
            metadata.GetOrAddString("ForwardedType"),
            assemblyReference,
            1);
        metadata.AddExportedType(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("NestedForwardedType"),
            forwardedType,
            2);
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

    private static MetadataReaderProvider CreateDeepExportedTypeMetadataReaderProvider(
        int depth,
        out ExportedTypeHandle startHandle)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticForwarderChain.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("SyntheticForwarderChain"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);

        EntityHandle implementation = metadata.AddAssemblyReference(
            metadata.GetOrAddString("Forwarded.Library"),
            new Version(2, 0, 0, 0),
            default,
            default,
            0,
            default);

        startHandle = default;
        for (int index = 0; index < depth; index++)
        {
            startHandle = metadata.AddExportedType(
                index == 0 ? TypeAttributes.Public | ForwarderTypeAttribute : TypeAttributes.NestedPublic,
                default,
                metadata.GetOrAddString("ForwardedType" + index),
                implementation,
                index + 1);
            implementation = startHandle;
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

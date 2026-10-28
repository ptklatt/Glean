using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;

namespace Glean.Tests.Contexts;

public sealed class ContextFlagBehaviorTests
{
    [Fact]
    public void MethodFlags_AllAttributeBitPatterns_MatchRawMasks()
    {
        var metadata = CreateMetadata();
        metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Owner"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        for (int bits = 0; bits <= ushort.MaxValue; bits++)
        {
            metadata.AddMethodDefinition((MethodAttributes)bits, MethodImplAttributes.Runtime,
                metadata.GetOrAddString("Method"), default, 0, MetadataTokens.ParameterHandle(1));
        }
        using var provider = Serialize(metadata);
        var reader = provider.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var value = MethodContext.Create(reader, handle);
            var flags = reader.GetMethodDefinition(handle).Attributes;
            var access = flags & MethodAttributes.MemberAccessMask;
            int expected = (access == MethodAttributes.Public ? 1 : 0) |
                (access == MethodAttributes.Private ? 2 : 0) |
                ((flags & MethodAttributes.Static) != 0 ? 4 : 0) |
                ((flags & MethodAttributes.Virtual) != 0 ? 8 : 0) |
                ((flags & MethodAttributes.Abstract) != 0 ? 16 : 0) |
                ((flags & MethodAttributes.Final) != 0 ? 32 : 0) |
                ((flags & MethodAttributes.SpecialName) != 0 ? 64 : 0);
            int actual = (value.IsPublic ? 1 : 0) | (value.IsPrivate ? 2 : 0) |
                (value.IsStatic ? 4 : 0) | (value.IsVirtual ? 8 : 0) |
                (value.IsAbstract ? 16 : 0) | (value.IsFinal ? 32 : 0) | (value.IsSpecialName ? 64 : 0);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void TypeFlags_AllVisibilityAndKindCombinations_MatchRawMasks()
    {
        var metadata = CreateMetadata();
        for (int bits = 0; bits < 256; bits++)
        {
            var flags = (TypeAttributes)(bits & 7) |
                ((bits & 8) != 0 ? TypeAttributes.Interface : 0) |
                ((bits & 16) != 0 ? TypeAttributes.Abstract : 0) |
                ((bits & 32) != 0 ? TypeAttributes.Sealed : 0) |
                ((bits & 64) != 0 ? TypeAttributes.SpecialName : 0) |
                ((bits & 128) != 0 ? TypeAttributes.BeforeFieldInit : 0);
            metadata.AddTypeDefinition(flags, default, metadata.GetOrAddString("Type" + bits), default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        }
        using var provider = Serialize(metadata);
        var reader = provider.GetMetadataReader();
        foreach (var handle in reader.TypeDefinitions)
        {
            var value = TypeContext.Create(reader, handle);
            var flags = reader.GetTypeDefinition(handle).Attributes;
            var visibility = flags & TypeAttributes.VisibilityMask;
            int expected = (visibility == TypeAttributes.Public ? 1 : 0) |
                (visibility == TypeAttributes.NestedPublic ? 2 : 0) |
                ((visibility >= TypeAttributes.NestedPublic) && (visibility <= TypeAttributes.NestedFamORAssem) ? 4 : 0) |
                ((flags & TypeAttributes.Interface) != 0 ? 8 : 0) |
                ((flags & TypeAttributes.Abstract) != 0 ? 16 : 0) |
                ((flags & TypeAttributes.Sealed) != 0 ? 32 : 0) |
                ((flags & (TypeAttributes.Abstract | TypeAttributes.Sealed)) == (TypeAttributes.Abstract | TypeAttributes.Sealed) ? 64 : 0);
            int actual = (value.IsPublic ? 1 : 0) | (value.IsNestedPublic ? 2 : 0) |
                (value.IsNested ? 4 : 0) | (value.IsInterface ? 8 : 0) |
                (value.IsAbstract ? 16 : 0) | (value.IsSealed ? 32 : 0) | (value.IsStaticClass ? 64 : 0);
            Assert.Equal(expected, actual);
        }
    }

    private static MetadataBuilder CreateMetadata()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("flags"), default, default, default);
        return metadata;
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
    }
}

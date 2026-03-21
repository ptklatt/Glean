using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class TypeSpecificationExtensionsTests
{
    // == Decode ==============================================================

    [Fact]
    public void DecodeTypeSpecification_GenericBaseType_ReturnsGenericInstanceSignature()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeSpecificationExtensionsTests).Assembly);
        var handle = GetGenericBaseTypeSpecificationHandle(metadata);

        TypeSignature signature = handle.DecodeTypeSpecification(metadata.Reader);

        var genericInstance = Assert.IsType<GenericInstanceSignature>(signature);
        Assert.True(genericInstance.GenericType.Is("System.Collections.Generic", "List`1"));

        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(genericInstance.Arguments));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);
    }

    // == Generic instantiation ==============================================

    [Fact]
    public void GenericInstantiationHelpers_GenericBaseType_ReportExpectedValues()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeSpecificationExtensionsTests).Assembly);
        var handle = GetGenericBaseTypeSpecificationHandle(metadata);

        Assert.True(handle.IsGenericInstantiation(metadata.Reader));
        Assert.False(handle.IsArrayType(metadata.Reader));
        Assert.False(handle.IsPointerType(metadata.Reader));
        Assert.False(handle.IsByRefType(metadata.Reader));
        Assert.Null(handle.GetElementType(metadata.Reader));

        var arguments = handle.GetGenericArguments(metadata.Reader);
        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(Assert.NotNull(arguments)));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);
    }

    // == Compound types ======================================================

    [Fact]
    public void ArrayHelpers_SyntheticArrayType_ReportExpectedValues()
    {
        using var provider = CreateTypeSpecificationMetadata(CreateArrayTypeSpecificationBlob(), out var handle);
        var reader = provider.GetMetadataReader();

        Assert.True(handle.IsArrayType(reader));
        Assert.False(handle.IsGenericInstantiation(reader));
        Assert.False(handle.IsPointerType(reader));
        Assert.False(handle.IsByRefType(reader));
        Assert.Null(handle.GetGenericArguments(reader));

        TypeSignature decoded = handle.DecodeTypeSpecification(reader);
        TypeSignature? elementType = handle.GetElementType(reader);

        Assert.NotNull(elementType);
        Assert.True(elementType!.Equals(GetElementType(decoded)));
    }

    [Fact]
    public void PointerHelpers_SyntheticPointerType_ReportExpectedValues()
    {
        using var provider = CreateTypeSpecificationMetadata(CreatePointerTypeSpecificationBlob(), out var handle);
        var reader = provider.GetMetadataReader();

        Assert.True(handle.IsPointerType(reader));
        Assert.False(handle.IsGenericInstantiation(reader));
        Assert.False(handle.IsArrayType(reader));
        Assert.False(handle.IsByRefType(reader));
        Assert.Null(handle.GetGenericArguments(reader));

        TypeSignature decoded = handle.DecodeTypeSpecification(reader);
        TypeSignature? elementType = handle.GetElementType(reader);

        Assert.NotNull(elementType);
        Assert.True(elementType!.Equals(GetElementType(decoded)));
    }

    [Fact]
    public void ByRefHelpers_SyntheticByRefType_ReportExpectedValues()
    {
        using var provider = CreateTypeSpecificationMetadata(CreateByRefTypeSpecificationBlob(), out var handle);
        var reader = provider.GetMetadataReader();

        Assert.True(handle.IsByRefType(reader));
        Assert.False(handle.IsGenericInstantiation(reader));
        Assert.False(handle.IsArrayType(reader));
        Assert.False(handle.IsPointerType(reader));
        Assert.Null(handle.GetGenericArguments(reader));

        TypeSignature decoded = handle.DecodeTypeSpecification(reader);
        TypeSignature? elementType = handle.GetElementType(reader);

        Assert.NotNull(elementType);
        Assert.True(elementType!.Equals(GetElementType(decoded)));
    }

    private static TypeSpecificationHandle GetGenericBaseTypeSpecificationHandle(MetadataScope metadata)
    {
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "TypeSpecificationGenericBaseFixture");
        Assert.Equal(HandleKind.TypeSpecification, typeDef.BaseType.Kind);
        return (TypeSpecificationHandle)typeDef.BaseType;
    }

    private static MetadataReaderProvider CreateTypeSpecificationMetadata(
        BlobBuilder signatureBuilder,
        out TypeSpecificationHandle handle)
    {
        var metadata = CreateSyntheticMetadataBuilder();
        handle = metadata.AddTypeSpecification(metadata.GetOrAddBlob(signatureBuilder));
        return CreateMetadataReaderProvider(metadata);
    }

    private static BlobBuilder CreateArrayTypeSpecificationBlob()
    {
        var blob = new BlobBuilder();
        var elementType = new BlobEncoder(blob).TypeSpecificationSignature().SZArray();
        elementType.Int32();
        return blob;
    }

    private static BlobBuilder CreatePointerTypeSpecificationBlob()
    {
        var blob = new BlobBuilder();
        var elementType = new BlobEncoder(blob).TypeSpecificationSignature().Pointer();
        elementType.Int32();
        return blob;
    }

    private static BlobBuilder CreateByRefTypeSpecificationBlob()
    {
        var blob = new BlobBuilder();
        var elementType = new FieldTypeEncoder(blob).Type(isByRef: true);
        elementType.Int32();
        return blob;
    }

    private static MetadataBuilder CreateSyntheticMetadataBuilder()
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
        metadata.AddTypeDefinition(
            0,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static MetadataReaderProvider CreateMetadataReaderProvider(MetadataBuilder metadata)
    {
        var root = new MetadataRootBuilder(metadata);
        var image = new BlobBuilder();
        root.Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }

    private static TypeSignature? GetElementType(TypeSignature signature)
    {
        return signature switch
        {
            SZArraySignature szArray => szArray.ElementType,
            ArraySignature array     => array.ElementType,
            PointerSignature pointer => pointer.ElementType,
            ByRefSignature byRef     => byRef.ElementType,
            _ => null
        };
    }
}

public sealed class TypeSpecificationGenericBaseFixture : List<int>
{
}

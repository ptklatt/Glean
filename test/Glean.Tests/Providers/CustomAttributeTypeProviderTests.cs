using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Xunit;

using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Providers;

public class CustomAttributeTypeProviderTests
{
    // == GetPrimitiveType ====================================================

    [Fact]
    public void GetPrimitiveType_Int32_ReturnsPrimitiveSignatureWithCorrectCode()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);

        var sig = provider.GetPrimitiveType(PrimitiveTypeCode.Int32);

        var primitive = Assert.IsType<PrimitiveTypeSignature>(sig);
        Assert.Equal(PrimitiveTypeCode.Int32, primitive.TypeCode);
    }

    [Fact]
    public void GetPrimitiveType_SameCode_ReturnsSameInstance()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);

        var first = provider.GetPrimitiveType(PrimitiveTypeCode.Boolean);
        var second = provider.GetPrimitiveType(PrimitiveTypeCode.Boolean);

        Assert.Same(first, second);
    }

    // == GetSystemType =======================================================

    [Fact]
    public void GetSystemType_ReturnsSerializedSystemType()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);

        var sig = provider.GetSystemType();

        var serialized = Assert.IsType<SerializedTypeNameSignature>(sig);
        Assert.Equal("System.Type", serialized.SerializedName);
        Assert.True(provider.IsSystemType(sig));
    }

    // == GetSZArrayType ======================================================

    [Fact]
    public void GetSZArrayType_WrapsElementType()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var elementType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.String);

        var sig = provider.GetSZArrayType(elementType);

        var array = Assert.IsType<SZArraySignature>(sig);
        Assert.Same(elementType, array.ElementType);
    }

    // == GetTypeFromDefinition ===============================================

    [Fact]
    public void GetTypeFromDefinition_ReturnsTypeDefinitionSignatureWithCorrectIdentity()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var handle = FindTypeDefinition(metadata.Reader, "System", "String");
        Assert.False(handle.IsNil, "Could not locate System.String TypeDefinition in CoreLib.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromDefinition(metadata.Reader, handle, 0);

        var typeDef = Assert.IsType<TypeDefinitionSignature>(sig);
        Assert.Equal(TypeSignatureKind.TypeDefinition, typeDef.Kind);
        Assert.True(typeDef.Is("System", "String"));
    }

    // == GetTypeFromReference ================================================

    [Fact]
    public void GetTypeFromReference_ReturnsTypeReferenceSignatureWithCorrectIdentity()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(CustomAttributeTypeProviderTests).Assembly);
        var handle = FindTypeReference(metadata.Reader, "System.Reflection.Metadata", "PrimitiveTypeCode");
        Assert.False(handle.IsNil, "Could not locate PrimitiveTypeCode TypeReference in test assembly.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromReference(metadata.Reader, handle, 0);

        var typeRef = Assert.IsType<TypeReferenceSignature>(sig);
        Assert.Equal(TypeSignatureKind.TypeReference, typeRef.Kind);
        Assert.True(typeRef.Is("System.Reflection.Metadata", "PrimitiveTypeCode"));
    }

    // == GetTypeFromSerializedName ===========================================

    [Fact]
    public void GetTypeFromSerializedName_ReturnsSerializedTypeNameSignature()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);

        var sig = provider.GetTypeFromSerializedName("System.String, System.Private.CoreLib");

        var serialized = Assert.IsType<SerializedTypeNameSignature>(sig);
        Assert.Equal("System.String, System.Private.CoreLib", serialized.SerializedName);
    }

    // == GetUnderlyingEnumType ===============================================

    [Fact]
    public void GetUnderlyingEnumType_SerializedName_ReturnsUnderlyingPrimitiveType()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromSerializedName("System.AttributeTargets");

        var result = provider.GetUnderlyingEnumType(sig);

        Assert.Equal(PrimitiveTypeCode.Int32, result);
    }

    [Fact]
    public void GetUnderlyingEnumType_SerializedName_AssemblyQualifiedName_StripsAssemblyQualifier()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromSerializedName(
            "System.AttributeTargets, System.Runtime, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        var result = provider.GetUnderlyingEnumType(sig);

        Assert.Equal(PrimitiveTypeCode.Int32, result);
    }

    [Fact]
    public void GetUnderlyingEnumType_SerializedName_NonEnum_ThrowsBadImageFormatException()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromSerializedName("System.String");

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(sig));
    }

    [Fact]
    public void GetUnderlyingEnumType_SerializedName_UnknownType_ThrowsOnRepeatedCall()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromSerializedName("System.DoesNotExistEnum99999");

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(sig));
        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(sig));
    }

    [Fact]
    public void GetUnderlyingEnumType_TypeDefinition_ReturnsUnderlyingPrimitiveType()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var handle = FindTypeDefinition(metadata.Reader, "System", "AttributeTargets");
        Assert.False(handle.IsNil, "Could not locate System.AttributeTargets TypeDefinition in CoreLib.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromDefinition(metadata.Reader, handle, 0);

        var result = provider.GetUnderlyingEnumType(sig);

        Assert.Equal(PrimitiveTypeCode.Int32, result);
    }

    [Fact]
    public void GetUnderlyingEnumType_TypeDefinition_NonEnum_ThrowsBadImageFormatException()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var handle = FindTypeDefinition(metadata.Reader, "System", "String");
        Assert.False(handle.IsNil, "Could not locate System.String TypeDefinition in CoreLib.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromDefinition(metadata.Reader, handle, 0);

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(sig));
    }

    [Fact]
    public void GetUnderlyingEnumType_TypeReference_WithoutResolver_ThrowsBadImageFormatException()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(CustomAttributeTypeProviderTests).Assembly);
        var handle = FindTypeReference(metadata.Reader, "System.Reflection.Metadata", "PrimitiveTypeCode");
        Assert.False(handle.IsNil, "Could not locate PrimitiveTypeCode TypeReference in test assembly.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromReference(metadata.Reader, handle, 0);

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(sig));
    }

    [Fact]
    public void GetUnderlyingEnumType_TypeReference_UsesResolverResult()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(CustomAttributeTypeProviderTests).Assembly);
        var handle = FindTypeReference(metadata.Reader, "System.Reflection.Metadata", "PrimitiveTypeCode");
        Assert.False(handle.IsNil, "Could not locate PrimitiveTypeCode TypeReference in test assembly.");

        var resolver = new StubEnumResolver(PrimitiveTypeCode.Int32);
        var provider = new CustomAttributeTypeProvider(metadata.Reader, resolver);
        var sig = provider.GetTypeFromReference(metadata.Reader, handle, 0);

        var result = provider.GetUnderlyingEnumType(sig);

        Assert.Equal(PrimitiveTypeCode.Int32, result);
        Assert.Equal(1, resolver.CallCount);
        Assert.NotNull(resolver.LastTypeReference);
        Assert.True(resolver.LastTypeReference!.Is("System.Reflection.Metadata", "PrimitiveTypeCode"));
    }

    // == IsSystemType ========================================================

    [Fact]
    public void IsSystemType_TypeDefinition_SystemType_ReturnsTrue()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var handle = FindTypeDefinition(metadata.Reader, "System", "Type");
        Assert.False(handle.IsNil, "Could not locate System.Type TypeDefinition in CoreLib.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromDefinition(metadata.Reader, handle, 0);

        Assert.True(provider.IsSystemType(sig));
    }

    [Fact]
    public void IsSystemType_TypeReference_SystemType_ReturnsTrue()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(CustomAttributeTypeProviderTests).Assembly);
        var handle = FindTypeReference(metadata.Reader, "System", "Type");
        Assert.False(handle.IsNil, "Could not locate System.Type TypeReference in test assembly.");

        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var sig = provider.GetTypeFromReference(metadata.Reader, handle, 0);

        Assert.True(provider.IsSystemType(sig));
    }

    [Fact]
    public void IsSystemType_NonSystemType_ReturnsFalse()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var provider = new CustomAttributeTypeProvider(metadata.Reader);

        Assert.False(provider.IsSystemType(PrimitiveTypeSignature.Get(PrimitiveTypeCode.String)));
    }

    private static TypeDefinitionHandle FindTypeDefinition(MetadataReader reader, string ns, string name)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            if ((reader.GetString(typeDef.Namespace) == ns) &&
                (reader.GetString(typeDef.Name) == name))
            {
                return handle;
            }
        }

        return default;
    }

    private static TypeReferenceHandle FindTypeReference(MetadataReader reader, string ns, string name)
    {
        int count = reader.GetTableRowCount(TableIndex.TypeRef);
        for (int row = 1; row <= count; row++)
        {
            var handle = MetadataTokens.TypeReferenceHandle(row);
            var typeRef = reader.GetTypeReference(handle);
            if ((reader.GetString(typeRef.Namespace) == ns) &&
                (reader.GetString(typeRef.Name) == name))
            {
                return handle;
            }
        }

        return default;
    }
    private sealed class StubEnumResolver : IEnumResolver
    {
        private readonly PrimitiveTypeCode? _result;

        public StubEnumResolver(PrimitiveTypeCode? result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public TypeReferenceSignature? LastTypeReference { get; private set; }

        public PrimitiveTypeCode? Resolve(TypeReferenceSignature typeRef)
        {
            CallCount++;
            LastTypeReference = typeRef;
            return _result;
        }
    }
}

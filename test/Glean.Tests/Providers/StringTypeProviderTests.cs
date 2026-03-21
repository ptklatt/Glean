using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Xunit;

using Glean.Providers;
using Glean.Tests.Utility;

namespace Glean.Tests.Providers;

public class StringTypeProviderTests
{
    private static readonly StringTypeProvider Provider = StringTypeProvider.Instance;

    // == Primitive types =====================================================

    [Fact]
    public void GetPrimitiveType_Int32_ReturnsCSharpAlias()
    {
        Assert.Equal("int", Provider.GetPrimitiveType(PrimitiveTypeCode.Int32));
    }

    [Fact]
    public void GetPrimitiveType_UnknownCode_FallsBackToEnumToString()
    {
        var unknown = (PrimitiveTypeCode)0xFF;
        Assert.Equal(unknown.ToString(), Provider.GetPrimitiveType(unknown));
    }

    // == Array types =========================================================

    [Fact]
    public void GetArrayType_Rank1_ReturnsSingleStarBracket()
    {
        var shape = new ArrayShape(rank: 1, sizes: ImmutableArray<int>.Empty, lowerBounds: ImmutableArray<int>.Empty);
        Assert.Equal("int[*]", Provider.GetArrayType("int", shape));
    }

    [Theory]
    [InlineData(2, "int[,]")]
    [InlineData(3, "int[,,]")]
    public void GetArrayType_MultiRank_ProducesCorrectCommaCount(int rank, string expected)
    {
        var shape = new ArrayShape(rank: rank, sizes: ImmutableArray<int>.Empty, lowerBounds: ImmutableArray<int>.Empty);
        Assert.Equal(expected, Provider.GetArrayType("int", shape));
    }

    [Fact]
    public void GetSZArrayType_ReturnsBracketSuffix()
    {
        Assert.Equal("string[]", Provider.GetSZArrayType("string"));
    }

    // == Reference and pointer types =========================================

    [Fact]
    public void GetByReferenceType_PrefixesRef()
    {
        Assert.Equal("ref int", Provider.GetByReferenceType("int"));
    }

    [Fact]
    public void GetPointerType_AppendsPointerSuffix()
    {
        Assert.Equal("byte*", Provider.GetPointerType("byte"));
    }

    [Fact]
    public void GetPinnedType_ReturnsElementTypeUnchanged()
    {
        Assert.Equal("int", Provider.GetPinnedType("int"));
    }

    // == Generic types =======================================================

    [Fact]
    public void GetGenericInstantiation_MetadataName_ProducesAngleBracketSyntax()
    {
        var args = ImmutableArray.Create("string", "int");
        Assert.Equal("Dictionary<string, int>", Provider.GetGenericInstantiation("Dictionary`2", args));
    }

    [Fact]
    public void GetGenericTypeParameter_ReturnsIndexedTypeParameterName()
    {
        Assert.Equal("T2", Provider.GetGenericTypeParameter(StringTypeProvider.EmptyContext, 2));
    }

    [Fact]
    public void GetGenericMethodParameter_ReturnsIndexedMethodParameterName()
    {
        Assert.Equal("TM1", Provider.GetGenericMethodParameter(StringTypeProvider.EmptyContext, 1));
    }

    // == Type references =====================================================

    [Fact]
    public void GetTypeFromDefinition_ReturnsNamespaceQualifiedName()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var handle = FindTypeDefinition(reader, "System", "String");
        Assert.False(handle.IsNil, "Could not locate System.String TypeDefinition in CoreLib.");

        Assert.Equal("System.String", Provider.GetTypeFromDefinition(reader, handle, 0));
    }

    [Fact]
    public void GetTypeFromReference_ReturnsNamespaceQualifiedName()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(StringTypeProvider).Assembly);
        var reader = metadata.Reader;

        var handle = FindTypeReference(reader, "System", "Object");
        Assert.False(handle.IsNil, "Could not locate System.Object TypeReference.");

        Assert.Equal("System.Object", Provider.GetTypeFromReference(reader, handle, 0));
    }

    [Fact]
    public void Decode_ListGetRange_ReturnTypeUsesTypeSpecFormatting()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var methodInfo = typeof(List<>).GetMethod("GetRange", new[] { typeof(int), typeof(int) });
        Assert.NotNull(methodInfo);

        var sig = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, StringTypeProvider.EmptyContext);

        Assert.Equal("System.Collections.Generic.List<T0>", sig.ReturnType);
    }

    // == Modified types ======================================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetModifiedType_IgnoresModifierAndReturnsUnderlyingType(bool isRequired)
    {
        Assert.Equal("int", Provider.GetModifiedType("mod", "int", isRequired));
    }

    // == Function pointers ===================================================

    [Fact]
    public void GetFunctionPointerType_NoParams_ReturnTypeOnly()
    {
        var sig = new MethodSignature<string>(
            header: default,
            returnType: "void",
            requiredParameterCount: 0,
            genericParameterCount: 0,
            parameterTypes: ImmutableArray<string>.Empty);

        Assert.Equal("delegate*<void>", Provider.GetFunctionPointerType(sig));
    }

    [Fact]
    public void GetFunctionPointerType_WithParams_ReturnTypeTrails()
    {
        var sig = new MethodSignature<string>(
            header: default,
            returnType: "bool",
            requiredParameterCount: 2,
            genericParameterCount: 0,
            parameterTypes: ImmutableArray.Create("int", "string"));

        Assert.Equal("delegate*<int, string, bool>", Provider.GetFunctionPointerType(sig));
    }

    // == IsSystemType ========================================================

    [Fact]
    public void GetSystemType_ReturnsSystemTypeName()
    {
        Assert.Equal("System.Type", Provider.GetSystemType());
    }

    [Theory]
    [InlineData("System.Type", true)]
    [InlineData("Type",        true)]
    [InlineData("string",      false)]
    [InlineData("Object",      false)]
    public void IsSystemType_AcceptsFullAndShortName_RejectsOthers(string type, bool expected)
    {
        Assert.Equal(expected, Provider.IsSystemType(type));
    }

    [Fact]
    public void GetTypeFromSerializedName_ReturnsNameUnchanged()
    {
        Assert.Equal("System.String, System.Private.CoreLib", Provider.GetTypeFromSerializedName("System.String, System.Private.CoreLib"));
    }

    [Fact]
    public void GetUnderlyingEnumType_AlwaysReturnsInt32()
    {
        Assert.Equal(PrimitiveTypeCode.Int32, Provider.GetUnderlyingEnumType("Any.Enum"));
    }

    // == FormatMethodSignature (integration via BCL metadata) ================

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

    private static MethodDefinition GetMethodDefinition(MetadataReader reader, MethodInfo method)
    {
        return reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(method.MetadataToken));
    }

    [Fact]
    public void FormatMethodSignature_ListAdd_DecodesGenericParameter()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var itemType = typeof(List<>).GetGenericArguments()[0];
        var methodInfo = typeof(List<>).GetMethod("Add", new[] { itemType });
        Assert.NotNull(methodInfo);

        var sig = StringTypeProvider.FormatMethodSignature(reader, GetMethodDefinition(reader, methodInfo!), includeReturnType: true);
        Assert.Equal("void Add(T0)", sig);
    }

    [Fact]
    public void FormatMethodSignature_StringContains_DecodesPrimitiveTypes()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var methodInfo = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
        Assert.NotNull(methodInfo);

        var sig = StringTypeProvider.FormatMethodSignature(reader, GetMethodDefinition(reader, methodInfo!), includeReturnType: true);
        Assert.Equal("bool Contains(string)", sig);
    }

    [Fact]
    public void FormatMethodSignature_WithoutReturnType_OmitsReturnType()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var itemType = typeof(List<>).GetGenericArguments()[0];
        var methodInfo = typeof(List<>).GetMethod("Add", new[] { itemType });
        Assert.NotNull(methodInfo);

        var sig = StringTypeProvider.FormatMethodSignature(reader, GetMethodDefinition(reader, methodInfo!), includeReturnType: false);
        Assert.Equal("Add(T0)", sig);
    }
}

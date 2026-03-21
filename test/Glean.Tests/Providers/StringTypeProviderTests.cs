using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Glean.Providers;
using Xunit;
using Xunit.Abstractions;

namespace Glean.Tests.Providers;

public class StringTypeProviderTests(ITestOutputHelper output)
{
    private static readonly StringTypeProvider Provider = StringTypeProvider.Instance;

    // == Primitive types ======================================================

    [Fact]
    public void GetPrimitiveType_UnknownCode_FallsBackToEnumToString()
    {
        var unknown = (PrimitiveTypeCode)0xFF;
        Assert.Equal(unknown.ToString(), Provider.GetPrimitiveType(unknown));
    }

    // == Array types ==========================================================

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

    // == Generic types ========================================================

    [Fact]
    public void GetGenericInstantiation_MetadataName_ProducesAngleBracketSyntax()
    {
        var args = ImmutableArray.Create("string", "int");
        Assert.Equal("Dictionary<string, int>", Provider.GetGenericInstantiation("Dictionary`2", args));
    }

    // == Function pointers ====================================================

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

    // == IsSystemType =========================================================

    [Theory]
    [InlineData("System.Type", true)]
    [InlineData("Type",        true)]
    [InlineData("string",      false)]
    [InlineData("Object",      false)]
    public void IsSystemType_AcceptsFullAndShortName_RejectsOthers(string type, bool expected)
    {
        Assert.Equal(expected, Provider.IsSystemType(type));
    }

    // == FormatMethodSignature (integration via BCL metadata) =================

    private static MethodDefinition? FindMethod(MetadataReader reader, string typeName, string methodName)
    {
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(typeHandle);
            if (reader.GetString(typeDef.Name) != typeName) { continue; }

            foreach (var methodHandle in typeDef.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if (reader.GetString(method.Name) == methodName)
                {
                    return method;
                }
            }
        }
        return null;
    }

    [Fact]
    public void FormatMethodSignature_ListAdd_DecodesGenericParameter()
    {
        using var pe = new PEReader(File.OpenRead(typeof(List<>).Assembly.Location));
        var reader = pe.GetMetadataReader();

        var method = FindMethod(reader, "List`1", "Add");
        Assert.True(method.HasValue, "Could not locate List<T>.Add in CoreLib metadata.");

        var sig = StringTypeProvider.FormatMethodSignature(reader, method.Value, includeReturnType: true);
        output.WriteLine(sig);
        Assert.Equal("void Add(T0)", sig);
    }

    [Fact]
    public void FormatMethodSignature_StringContains_DecodesPrimitiveTypes()
    {
        using var pe = new PEReader(File.OpenRead(typeof(string).Assembly.Location));
        var reader = pe.GetMetadataReader();

        var method = FindMethod(reader, "String", "Contains");
        Assert.True(method.HasValue, "Could not locate string.Contains in CoreLib metadata.");

        var sig = StringTypeProvider.FormatMethodSignature(reader, method.Value, includeReturnType: true);
        Assert.Equal("bool Contains(string)", sig);
    }

    [Fact]
    public void FormatMethodSignature_WithoutReturnType_OmitsReturnType()
    {
        using var pe = new PEReader(File.OpenRead(typeof(List<>).Assembly.Location));
        var reader = pe.GetMetadataReader();

        var method = FindMethod(reader, "List`1", "Add");
        Assert.True(method.HasValue);

        var sig = StringTypeProvider.FormatMethodSignature(reader, method.Value, includeReturnType: false);
        Assert.Equal("Add(T0)", sig);
    }
}

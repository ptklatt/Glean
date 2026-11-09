using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Decoding;
using Glean.Providers;
using Glean.Tests.Utility;

namespace Glean.Tests.Decoding;

public sealed class NestedEnumAttributeTests
{
    private const string Source = """
        using System;

        namespace Fixture;

        public enum TopValue { One = 1 }
        public class First
        {
            public enum Value : byte { One = 1 }
            public class Inner
            {
                public enum Value : long { Minus = -2 }
            }
        }
        public class Second
        {
            public enum Value : ulong { Maximum = ulong.MaxValue }
        }
        public sealed class MarkerAttribute : Attribute
        {
            public First.Value Property { get; set; }
            public First.Inner.Value Field;
            public object Boxed { get; set; } = First.Value.One;
            public Second.Value[] Values { get; set; } = Array.Empty<Second.Value>();
        }
        [Marker(Property = First.Value.One, Field = First.Inner.Value.Minus,
            Boxed = First.Value.One, Values = new[] { Second.Value.Maximum })]
        public class Target { }
        """;

    public static IEnumerable<object[]> SerializedNames()
    {
        foreach (bool qualified in new[] { false, true })
        {
            yield return new object[] { "Fixture.TopValue", PrimitiveTypeCode.Int32, qualified };
            yield return new object[] { "Fixture.First+Value", PrimitiveTypeCode.Byte, qualified };
            yield return new object[] { "Fixture.First+Inner+Value", PrimitiveTypeCode.Int64, qualified };
            yield return new object[] { "Fixture.Second+Value", PrimitiveTypeCode.UInt64, qualified };
        }
    }

    [Fact]
    public void Decode_LocalNestedEnumNamedArguments_PreservesKindsAndValues()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var type = metadata.GetTypeDefinition("Fixture", "Target");
        var attribute = Assert.Single(type.GetCustomAttributes());

        var decoded = CustomAttributeDecoder.Decode(metadata.Reader, attribute);

        Assert.Empty(decoded.FixedArguments);
        Assert.Equal(4, decoded.NamedArguments.Length);
        var property = Assert.Single(decoded.NamedArguments.Where(value => value.Name == "Property"));
        Assert.Equal(CustomAttributeNamedArgumentKind.Property, property.Kind);
        Assert.Equal((byte)1, Assert.IsType<byte>(property.Value.Value));
        var field = Assert.Single(decoded.NamedArguments.Where(value => value.Name == "Field"));
        Assert.Equal(CustomAttributeNamedArgumentKind.Field, field.Kind);
        Assert.Equal(-2L, Assert.IsType<long>(field.Value.Value));
        var boxed = Assert.Single(decoded.NamedArguments.Where(value => value.Name == "Boxed"));
        Assert.Equal((byte)1, Assert.IsType<byte>(boxed.Value.Value));
        var array = Assert.Single(decoded.NamedArguments.Where(value => value.Name == "Values"));
        var element = Assert.Single(array.Value.GetArrayElements());
        Assert.Equal(ulong.MaxValue, Assert.IsType<ulong>(element.Value));
    }

    [Theory]
    [MemberData(nameof(SerializedNames))]
    public void GetUnderlyingEnumType_LocalNames_PreservesDeclaringTypesAndQualifierHandling(
        string name, PrimitiveTypeCode expected, bool qualified)
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        if (qualified)
        {
            name += ", " + metadata.Reader.GetString(metadata.Reader.GetAssemblyDefinition().Name);
        }
        var signature = provider.GetTypeFromSerializedName(name);

        Assert.Equal(expected, provider.GetUnderlyingEnumType(signature));
        Assert.Equal(expected, provider.GetUnderlyingEnumType(signature));
    }

    [Theory]
    [InlineData("Fixture.First+Missing")]
    [InlineData("Fixture.Missing+Value")]
    [InlineData("fixture.First+Value")]
    [InlineData("Fixture.First.Value")]
    [InlineData("Fixture.First+Inner")]
    public void GetUnderlyingEnumType_MissingOrNonEnumNestedName_ThrowsOnRepeatedCall(string name)
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        var signature = provider.GetTypeFromSerializedName(name);

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(signature));
        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(signature));
    }

    [Fact]
    public void GetUnderlyingEnumType_DeepLocalNestedEnum_DecodesWithoutAnArbitraryDepthLimit()
    {
        string source = "namespace Fixture {" + string.Concat(Enumerable.Range(0, 32).Select(i => "public class N" + i + " {")) +
            "public enum Value : short { One = 1 }" + new string('}', 33);
        using var metadata = TestUtility.BuildMetadata(source);
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        string name = "Fixture." + string.Join("+", Enumerable.Range(0, 32).Select(i => "N" + i)) + "+Value";

        Assert.Equal(PrimitiveTypeCode.Int16, provider.GetUnderlyingEnumType(provider.GetTypeFromSerializedName(name)));
    }

    [Theory]
    [InlineData("Fixture.First+Value[]")]
    [InlineData("Fixture.First+Value*")]
    [InlineData("Fixture.First+Value&")]
    public void GetUnderlyingEnumType_CompositeName_ThrowsBadImageFormat(string name)
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var provider = new CustomAttributeTypeProvider(metadata.Reader);

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(provider.GetTypeFromSerializedName(name)));
    }

    [Fact]
    public void GetUnderlyingEnumType_DeepGenericName_ThrowsWithoutRecursiveParsing()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var provider = new CustomAttributeTypeProvider(metadata.Reader);
        string name = string.Concat(Enumerable.Repeat("G[", 50000)) + "E" + new string(']', 50000);

        Assert.Throws<BadImageFormatException>(() => provider.GetUnderlyingEnumType(provider.GetTypeFromSerializedName(name)));
    }

    [Theory]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData('*')]
    [InlineData('&')]
    [InlineData('+')]
    public void GetUnderlyingEnumType_EscapedCharacterInDeclaringName_Decodes(char character)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("EscapedEnum"), default, default, default);
        metadata.AddAssembly(metadata.GetOrAddString("EscapedEnum"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        var core = typeof(Enum).Assembly.GetName();
        var reference = metadata.AddAssemblyReference(metadata.GetOrAddString(core.Name!), core.Version!, default,
            metadata.GetOrAddBlob(core.GetPublicKeyToken()!), 0, default);
        var objectType = metadata.AddTypeReference(reference, metadata.GetOrAddString("System"), metadata.GetOrAddString("Object"));
        var enumType = metadata.AddTypeReference(reference, metadata.GetOrAddString("System"), metadata.GetOrAddString("Enum"));
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var outer = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"),
            metadata.GetOrAddString("Outer" + character + "Part"), objectType,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var nested = metadata.AddTypeDefinition(TypeAttributes.NestedPublic | TypeAttributes.Sealed, default,
            metadata.GetOrAddString("Value"), enumType, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddNestedType(nested, outer);
        metadata.AddFieldDefinition(FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
            metadata.GetOrAddString("value__"), metadata.GetOrAddBlob(new byte[] { 6, 7 }));
        var blob = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(blob, 0, 0);
        using var readerProvider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(blob.ToArray()));
        var provider = new CustomAttributeTypeProvider(readerProvider.GetMetadataReader());
        string name = "Fixture.Outer\\" + character + "Part+Value";

        Assert.Equal(PrimitiveTypeCode.UInt16, provider.GetUnderlyingEnumType(provider.GetTypeFromSerializedName(name)));
    }
}

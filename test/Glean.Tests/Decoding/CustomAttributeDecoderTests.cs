using System.Reflection.Metadata;

using Xunit;

using Glean.Decoding;
using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Decoding;

public class CustomAttributeDecoderTests
{
    private const string LocalAttributeSource = """
        using System;

        namespace Glean.Tests.Decoding;

        [AttributeUsage(AttributeTargets.Class)]
        internal sealed class LocalDecoderFixtureAttribute : Attribute
        {
            public LocalDecoderFixtureAttribute(int number, string text, string[] names)
            {
            }

            public bool Flag;

            public string Note { get; set; } = string.Empty;
        }

        [LocalDecoderFixture(7, "hello", new[] { "alpha", "beta" }, Flag = true, Note = "named")]
        internal sealed class LocalAttributedType
        {
        }
        """;

    private const string EmptyAttributeSource = """
        using System;

        namespace Glean.Tests.Decoding;

        [AttributeUsage(AttributeTargets.Class)]
        internal sealed class EmptyDecoderFixtureAttribute : Attribute
        {
        }

        [EmptyDecoderFixture]
        internal sealed class EmptyAttributedType
        {
        }
        """;

    private const string FrameworkAttributeSource = """
        using System;

        namespace Glean.Tests.Decoding;

        [Obsolete("legacy")]
        internal sealed class FrameworkAttributedType
        {
        }
        """;

    private const string ExternalEnumAttributeSource = """
        using System;

        namespace Glean.Tests.Decoding;

        [AttributeUsage(AttributeTargets.Class)]
        internal sealed class ExternalEnumDecoderFixtureAttribute : Attribute
        {
            public ExternalEnumDecoderFixtureAttribute(AttributeTargets targets)
            {
            }
        }

        [ExternalEnumDecoderFixture(AttributeTargets.Class)]
        internal sealed class ExternalEnumAttributedType
        {
        }
        """;

    // == Decode ==============================================================

    [Fact]
    public void Decode_NullReader_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            "reader",
            () => CustomAttributeDecoder.Decode(null!, default));
    }

    [Fact]
    public void Decode_NilHandle_ThrowsArgumentException()
    {
        using var metadata = TestUtility.BuildMetadata(LocalAttributeSource);

        var ex = Assert.Throws<ArgumentException>(
            () => CustomAttributeDecoder.Decode(metadata.Reader, default));

        Assert.Equal("handle", ex.ParamName);
    }

    [Fact]
    public void Decode_LocalAttributeOnType_ReturnsTypeDefinitionAndDecodedFixedAndNamedArguments()
    {
        using var metadata = TestUtility.BuildMetadata(LocalAttributeSource);
        var attributeHandle = GetSingleAttributeHandle(metadata, "Glean.Tests.Decoding", "LocalAttributedType");

        var decoded = CustomAttributeDecoder.Decode(metadata.Reader, attributeHandle);

        var attributeType = Assert.IsType<TypeDefinitionSignature>(decoded.AttributeType);
        Assert.True(attributeType.Is("Glean.Tests.Decoding", "LocalDecoderFixtureAttribute"));

        Assert.Equal(3, decoded.FixedArguments.Length);

        var numberType = Assert.IsType<PrimitiveTypeSignature>(decoded.FixedArguments[0].Type);
        Assert.Equal(PrimitiveTypeCode.Int32, numberType.TypeCode);
        Assert.Equal(7, Assert.IsType<int>(decoded.FixedArguments[0].Value));

        var textType = Assert.IsType<PrimitiveTypeSignature>(decoded.FixedArguments[1].Type);
        Assert.Equal(PrimitiveTypeCode.String, textType.TypeCode);
        Assert.Equal("hello", Assert.IsType<string>(decoded.FixedArguments[1].Value));

        Assert.True(decoded.FixedArguments[2].IsArray);
        var arrayType = Assert.IsType<SZArraySignature>(decoded.FixedArguments[2].Type);
        var arrayElementType = Assert.IsType<PrimitiveTypeSignature>(arrayType.ElementType);
        Assert.Equal(PrimitiveTypeCode.String, arrayElementType.TypeCode);

        var arrayElements = decoded.FixedArguments[2].GetArrayElements();
        Assert.Collection(
            arrayElements,
            item =>
            {
                var type = Assert.IsType<PrimitiveTypeSignature>(item.Type);
                Assert.Equal(PrimitiveTypeCode.String, type.TypeCode);
                Assert.Equal("alpha", Assert.IsType<string>(item.Value));
            },
            item =>
            {
                var type = Assert.IsType<PrimitiveTypeSignature>(item.Type);
                Assert.Equal(PrimitiveTypeCode.String, type.TypeCode);
                Assert.Equal("beta", Assert.IsType<string>(item.Value));
            });

        Assert.Equal(2, decoded.NamedArguments.Length);

        var flag = Assert.Single(
            decoded.NamedArguments.Where(arg => arg.Name == "Flag"));
        Assert.Equal(CustomAttributeNamedArgumentKind.Field, flag.Kind);
        var flagType = Assert.IsType<PrimitiveTypeSignature>(flag.Type);
        Assert.Equal(PrimitiveTypeCode.Boolean, flagType.TypeCode);
        Assert.True(Assert.IsType<bool>(flag.Value.Value));

        var note = Assert.Single(
            decoded.NamedArguments.Where(arg => arg.Name == "Note"));
        Assert.Equal(CustomAttributeNamedArgumentKind.Property, note.Kind);
        var noteType = Assert.IsType<PrimitiveTypeSignature>(note.Type);
        Assert.Equal(PrimitiveTypeCode.String, noteType.TypeCode);
        Assert.Equal("named", Assert.IsType<string>(note.Value.Value));
    }

    [Fact]
    public void Decode_LocalAttributeWithoutArguments_ReturnsEmptyFixedAndNamedArguments()
    {
        using var metadata = TestUtility.BuildMetadata(EmptyAttributeSource);
        var attributeHandle = GetSingleAttributeHandle(metadata, "Glean.Tests.Decoding", "EmptyAttributedType");

        var decoded = CustomAttributeDecoder.Decode(metadata.Reader, attributeHandle);

        var attributeType = Assert.IsType<TypeDefinitionSignature>(decoded.AttributeType);
        Assert.True(attributeType.Is("Glean.Tests.Decoding", "EmptyDecoderFixtureAttribute"));
        Assert.Empty(decoded.FixedArguments);
        Assert.Empty(decoded.NamedArguments);
    }

    [Fact]
    public void Decode_FrameworkAttributeOnType_ReturnsTypeReferenceAndFixedArguments()
    {
        using var metadata = TestUtility.BuildMetadata(FrameworkAttributeSource);
        var attributeHandle = GetSingleAttributeHandle(metadata, "Glean.Tests.Decoding", "FrameworkAttributedType");

        var decoded = CustomAttributeDecoder.Decode(metadata.Reader, attributeHandle);

        var attributeType = Assert.IsType<TypeReferenceSignature>(decoded.AttributeType);
        Assert.True(attributeType.Is("System", "ObsoleteAttribute"));

        var fixedArg = Assert.Single(decoded.FixedArguments);
        var fixedArgType = Assert.IsType<PrimitiveTypeSignature>(fixedArg.Type);
        Assert.Equal(PrimitiveTypeCode.String, fixedArgType.TypeCode);
        Assert.Equal("legacy", Assert.IsType<string>(fixedArg.Value));
        Assert.Empty(decoded.NamedArguments);
    }

    [Fact]
    public void Decode_ExternalEnumArgumentWithoutResolver_ThrowsBadImageFormatException()
    {
        using var metadata = TestUtility.BuildMetadata(ExternalEnumAttributeSource);
        var attributeHandle = GetSingleAttributeHandle(metadata, "Glean.Tests.Decoding", "ExternalEnumAttributedType");

        Assert.Throws<BadImageFormatException>(() => CustomAttributeDecoder.Decode(metadata.Reader, attributeHandle));
    }

    [Fact]
    public void Decode_ExternalEnumArgumentWithResolver_ReturnsDecodedEnumValue()
    {
        using var metadata = TestUtility.BuildMetadata(ExternalEnumAttributeSource);
        var attributeHandle = GetSingleAttributeHandle(metadata, "Glean.Tests.Decoding", "ExternalEnumAttributedType");
        var resolver = new StubEnumResolver(PrimitiveTypeCode.Int32);

        var decoded = CustomAttributeDecoder.Decode(metadata.Reader, attributeHandle, resolver);

        var fixedArg = Assert.Single(decoded.FixedArguments);
        var fixedArgType = Assert.IsType<TypeReferenceSignature>(fixedArg.Type);
        Assert.True(fixedArgType.Is("System", "AttributeTargets"));
        Assert.Equal((int)AttributeTargets.Class, Assert.IsType<int>(fixedArg.Value));
        Assert.Equal(1, resolver.CallCount);
        Assert.NotNull(resolver.LastTypeReference);
        Assert.True(resolver.LastTypeReference!.Is("System", "AttributeTargets"));
    }

    private static CustomAttributeHandle GetSingleAttributeHandle(MetadataScope metadata, string ns, string name)
    {
        var targetType = metadata.GetTypeDefinition(ns, name);
        return Assert.Single(targetType.GetCustomAttributes());
    }
}

internal sealed class StubEnumResolver : IEnumResolver
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

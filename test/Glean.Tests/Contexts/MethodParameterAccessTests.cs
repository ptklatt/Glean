using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Extensions;
using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Contexts;

public sealed class MethodParameterAccessTests
{
    private const string ParameterSource = """
        using System;

        namespace Fixtures;

        [AttributeUsage(AttributeTargets.ReturnValue)]
        internal sealed class ReturnMarkerAttribute : Attribute
        {
        }

        internal static class ParameterFixture
        {
            internal static void Generic<T>(ref int input, out int output, int count = 7)
            {
                output = input + count;
            }

            [return: ReturnMarker]
            internal static int WithReturnMetadata()
            {
                return 1;
            }
        }
        """;

    [Fact]
    public void TryGetParameter_SparseUnorderedRows_UsesSignaturePositions()
    {
        using var provider = CreateMethodMetadata(3, (3, "third"), (0, "result"), (1, string.Empty));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        Assert.True(method.TryGetParameter(0, out var first));
        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal(string.Empty, first.Name);
        Assert.True(first.NameHandle.IsNil);
        Assert.Equal(method, first.DeclaringMethod);

        Assert.False(method.TryGetParameter(1, out var missing));
        Assert.False(missing.IsValid);

        Assert.True(method.TryGetParameter(2, out var third));
        Assert.Equal(3, third.SequenceNumber);
        Assert.Equal("third", third.Name);
    }

    [Fact]
    public void TryGetReturnParameter_ReturnRowNotFirst_FindsSequenceZero()
    {
        using var provider = CreateMethodMetadata(3, (3, "third"), (0, "result"), (1, "first"));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        Assert.True(method.TryGetReturnParameter(out var result));

        Assert.Equal(0, result.SequenceNumber);
        Assert.Equal("result", result.Name);
        Assert.Equal(method, result.DeclaringMethod);
    }

    [Fact]
    public void ParameterMetadata_MissingRows_ReturnsInvalidContexts()
    {
        using var provider = CreateMethodMetadata(2);
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        Assert.False(method.TryGetParameter(0, out var parameter));
        Assert.False(parameter.IsValid);
        Assert.False(method.TryGetReturnParameter(out var result));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void TryGetParameter_MetadataBeyondSignatureCount_ReturnsFalse()
    {
        using var provider = CreateMethodMetadata(1, (2, "outsideSignature"));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        Assert.False(method.TryGetParameter(1, out var parameter));
        Assert.False(parameter.IsValid);
    }

    [Fact]
    public void ParameterMetadata_OnlyReturnRow_DoesNotBecomeFirstParameter()
    {
        using var provider = CreateMethodMetadata(0, (0, "result"));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        Assert.False(method.TryGetParameter(0, out var parameter));
        Assert.False(parameter.IsValid);
        Assert.True(method.TryGetReturnParameter(out var result));
        Assert.Equal(0, result.SequenceNumber);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void TryGetParameter_NegativeIndex_ThrowsArgumentOutOfRange(int index)
    {
        using var provider = CreateMethodMetadata(1, (1, "value"));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => method.TryGetParameter(index, out _));

        Assert.Equal("index", exception.ParamName);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(65535)]
    [InlineData(int.MaxValue)]
    public void TryGetParameter_OutOfRangeOrOverflowIndex_ReturnsFalse(int index)
    {
        using var provider = CreateMethodMetadata(3, (1, "first"), (3, "third"));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));

        Assert.False(method.TryGetParameter(index, out var parameter));
        Assert.False(parameter.IsValid);
    }

    [Fact]
    public void TryGetParameter_GenericRefOutAndOptionalRows_MatchSignaturePositions()
    {
        using var metadata = TestUtility.BuildMetadata(ParameterSource);
        var method = FindMethod(metadata, "Generic");
        var signature = method.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);

        Assert.Equal(1, signature.GenericParameterCount);
        Assert.Equal(3, signature.ParameterTypes.Length);
        Assert.IsType<ByRefSignature>(signature.ParameterTypes[0]);
        Assert.IsType<ByRefSignature>(signature.ParameterTypes[1]);

        Assert.True(method.TryGetParameter(0, out var input));
        Assert.Equal("input", input.Name);
        Assert.False(input.IsOut);
        Assert.Equal(method, input.DeclaringMethod);

        Assert.True(method.TryGetParameter(1, out var output));
        Assert.Equal("output", output.Name);
        Assert.True(output.IsOut);

        Assert.True(method.TryGetParameter(2, out var optional));
        Assert.Equal("count", optional.Name);
        Assert.True(optional.IsOptional);
        Assert.True(optional.HasDefault);
        Assert.Equal(7, optional.Parameter.GetDefaultValue(metadata.Reader));
        Assert.False(method.TryGetReturnParameter(out _));
    }

    [Fact]
    public void TryGetReturnParameter_ReturnAttribute_IsAvailable()
    {
        using var metadata = TestUtility.BuildMetadata(ParameterSource);
        var method = FindMethod(metadata, "WithReturnMetadata");

        Assert.True(method.TryGetReturnParameter(out var result));

        Assert.Equal(0, result.SequenceNumber);
        Assert.True(result.HasAttribute("Fixtures", "ReturnMarkerAttribute"));
        Assert.Equal(method, result.DeclaringMethod);
    }

    [Fact]
    public void ParameterMetadata_RepeatedLookups_DoNotAllocate()
    {
        using var provider = CreateMethodMetadata(3, (3, "third"), (0, "result"), (1, "first"));
        var method = MethodContext.Create(provider.GetMetadataReader(), MetadataTokens.MethodDefinitionHandle(1));
        for (int i = 0; i < 32; i++)
        {
            ReadMetadata(method);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            ReadMetadata(method);
        }
        long allocations = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocations);
    }

    private static void ReadMetadata(MethodContext method)
    {
        if (!method.TryGetParameter(2, out var parameter) || (parameter.SequenceNumber != 3) ||
            !method.TryGetReturnParameter(out var result) || (result.SequenceNumber != 0))
        {
            throw new InvalidOperationException("Parameter metadata lookup returned an unexpected result.");
        }
    }

    private static MethodContext FindMethod(MetadataScope metadata, string name)
    {
        var type = metadata.GetTypeDefinition("Fixtures", "ParameterFixture");
        foreach (var handle in type.GetMethods())
        {
            var method = MethodContext.Create(metadata.Reader, handle);
            if (method.NameIs(name))
            {
                return method;
            }
        }
        throw new InvalidOperationException("Could not locate fixture method " + name);
    }

    private static MetadataReaderProvider CreateMethodMetadata(int parameterCount, params (int Sequence, string Name)[] rows)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Parameters.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("Parameters"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(0, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("ParameterFixture"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

        var signature = new BlobBuilder();
        signature.WriteByte((byte)SignatureCallingConvention.Default);
        signature.WriteCompressedInteger(parameterCount);
        signature.WriteByte((byte)SignatureTypeCode.Void);
        for (int i = 0; i < parameterCount; i++)
        {
            signature.WriteByte((byte)SignatureTypeCode.Int32);
        }
        metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
            metadata.GetOrAddString("Method"), metadata.GetOrAddBlob(signature), 0, MetadataTokens.ParameterHandle(1));
        foreach (var row in rows)
        {
            metadata.AddParameter(ParameterAttributes.None, metadata.GetOrAddString(row.Name), row.Sequence);
        }

        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }
}

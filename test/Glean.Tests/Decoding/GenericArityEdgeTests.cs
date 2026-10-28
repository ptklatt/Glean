using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

namespace Glean.Tests.Decoding;

public sealed class GenericArityEdgeTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04, 0x00 }, true, 0 };
        yield return new object[] { new byte[] { 0x15, 0x11, 0x04, 0x01 }, true, 1 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04, 0x80, 0x80 }, true, 128 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04, 0xc0, 0x00, 0x40, 0x00 }, true, 16384 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04, 0xdf, 0xff, 0xff, 0xff }, true, 0x1fffffff };
        yield return new object[] { new byte[] { 0x15, 0xff, 0x00, 0x01 }, true, 1 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x80, 0x00, 0x80, 0x01 }, true, 1 };
        yield return new object[] { Array.Empty<byte>(), false, 0 };
        yield return new object[] { new byte[] { 0x08, 0x12, 0x04, 0x01 }, false, 0 };
        yield return new object[] { new byte[] { 0x15 }, false, 0 };
        yield return new object[] { new byte[] { 0x15, 0x12 }, false, 0 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04 }, false, 0 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x80 }, false, 0 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04, 0x80 }, false, 0 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0xff, 0x01 }, false, 0 };
        yield return new object[] { new byte[] { 0x15, 0x12, 0x04, 0xff }, false, 0 };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TryGetGenericInstanceArity_Boundaries_PreservesResultAndCount(byte[] bytes, bool expected, int count)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("arities"), default, default, default);
        var handle = metadata.GetOrAddBlob(bytes);
        using var provider = Serialize(metadata);
        Assert.Equal(expected, SignatureInspector.TryGetGenericInstanceArity(provider.GetMetadataReader(), handle, out int actual));
        Assert.Equal(count, actual);
    }

    [Fact]
    public void TryGetGenericInstanceArity_InvalidHeapOffset_ReturnsFalseAndZero()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("arities"), default, default, default);
        using var provider = Serialize(metadata);
        Assert.False(SignatureInspector.TryGetGenericInstanceArity(provider.GetMetadataReader(), MetadataTokens.BlobHandle(0x00ffffff), out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void TryGetGenericInstanceArity_NullReader_PreservesNilAndNonNilBehavior()
    {
        Assert.False(SignatureInspector.TryGetGenericInstanceArity(null!, default, out int count));
        Assert.Equal(0, count);
        int unchanged = 17;
        Assert.Throws<NullReferenceException>(() => SignatureInspector.TryGetGenericInstanceArity(null!, MetadataTokens.BlobHandle(1), out unchanged));
        Assert.Equal(17, unchanged);
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
    }
}

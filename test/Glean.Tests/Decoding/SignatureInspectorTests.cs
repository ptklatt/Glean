using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

namespace Glean.Tests.Decoding;

public sealed class SignatureInspectorTests
{
    [Theory]
    [InlineData(new byte[] { 0x08 }, false, 0)]
    [InlineData(new byte[] { 0x08, 0x80 }, false, 0)]
    [InlineData(new byte[] { 0x08, 0xc0, 0x00, 0x00 }, false, 0)]
    [InlineData(new byte[] { 0x08, 0xff }, false, 0)]
    [InlineData(new byte[] { 0x08, 0x00 }, true, 0)]
    [InlineData(new byte[] { 0x08, 0x7f }, true, 127)]
    [InlineData(new byte[] { 0x08, 0x80, 0x00 }, true, 0)]
    [InlineData(new byte[] { 0x08, 0xbf, 0xff }, true, 16383)]
    [InlineData(new byte[] { 0x28, 0x81, 0x01 }, true, 257)]
    [InlineData(new byte[] { 0x00, 0xc0, 0x00, 0x40, 0x00 }, true, 16384)]
    [InlineData(new byte[] { 0x08, 0xdf, 0xff, 0xff, 0xff }, true, 536870911)]
    public void TryGetPropertyParameterCount_SignatureBytes_ReadsCountOrRejectsMalformedEncoding(byte[] bytes, bool expected, int count)
    {
        var metadata = CreateMetadata();
        var handle = metadata.GetOrAddBlob(bytes);
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());

        bool result = SignatureInspector.TryGetPropertyParameterCount(provider.GetMetadataReader(), handle, out int actual);

        Assert.Equal(expected, result);
        Assert.Equal(count, actual);
    }

    [Fact]
    public void TryGetPropertyParameterCount_NilHandle_ReturnsFalseWithoutReading()
    {
        Assert.False(SignatureInspector.TryGetPropertyParameterCount(null!, default, out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void TryGetPropertyParameterCount_InvalidHandle_ReturnsFalse()
    {
        var metadata = CreateMetadata();
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());

        Assert.False(SignatureInspector.TryGetPropertyParameterCount(provider.GetMetadataReader(), MetadataTokens.BlobHandle(0x00ffffff), out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void TryGetPropertyParameterCount_EmptyNonNilBlob_ReturnsFalse()
    {
        var metadata = CreateMetadata();
        var handle = metadata.GetOrAddBlob(new byte[] { 0, 0 });
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
        var emptyHandle = MetadataTokens.BlobHandle(MetadataTokens.GetHeapOffset(handle) + 1);

        Assert.False(SignatureInspector.TryGetPropertyParameterCount(provider.GetMetadataReader(), emptyHandle, out int count));
        Assert.Equal(0, count);
    }

    [Fact]
    public void TryGetPropertyParameterCount_NullReaderAndNonNilHandle_Throws()
    {
        Assert.Throws<NullReferenceException>(() => SignatureInspector.TryGetPropertyParameterCount(null!, MetadataTokens.BlobHandle(1), out _));
    }

    [Fact]
    public void TryGetPropertyParameterCount_VariedSignatureBlobs_MatchesPreviousReadBehavior()
    {
        var metadata = CreateMetadata();
        var handles = new List<BlobHandle>();
        for (int first = 0; first <= byte.MaxValue; first++)
        {
            for (int length = 1; length <= 5; length++)
            {
                var bytes = new byte[length];
                bytes[0] = 0x08;
                if (length > 1)
                {
                    bytes[1] = (byte)first;
                }

                handles.Add(metadata.GetOrAddBlob(bytes));
            }
        }

        var random = new Random(20261026);
        for (int i = 0; i < 4096; i++)
        {
            var bytes = new byte[random.Next(1, 9)];
            random.NextBytes(bytes);
            handles.Add(metadata.GetOrAddBlob(bytes));
        }

        handles.Add(metadata.GetOrAddBlob(new byte[] { 0x08, 0xdf, 0xff, 0xff, 0xff }));
        handles.Add(metadata.GetOrAddBlob(new byte[] { 0x08, 0x80, 0x00 }));
        handles.Add(metadata.GetOrAddBlob(new byte[] { 0x08, 0xc0, 0x00, 0x00, 0x00 }));
        handles.Add(default);
        handles.Add(MetadataTokens.BlobHandle(0x00ffffff));

        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
        var reader = provider.GetMetadataReader();

        foreach (var handle in handles)
        {
            bool expected = TryReadPrevious(reader, handle, out int expectedCount);
            bool actual = SignatureInspector.TryGetPropertyParameterCount(reader, handle, out int actualCount);
            Assert.Equal(expected, actual);
            Assert.Equal(expectedCount, actualCount);
        }
    }

    private static bool TryReadPrevious(MetadataReader reader, BlobHandle handle, out int count)
    {
        if (handle.IsNil)
        {
            count = 0;
            return false;
        }

        BlobReader blob;
        try
        {
            blob = reader.GetBlobReader(handle);
        }
        catch (BadImageFormatException)
        {
            count = 0;
            return false;
        }

        if (blob.RemainingBytes == 0)
        {
            count = 0;
            return false;
        }

        blob.ReadByte();
        if (blob.TryReadCompressedInteger(out count))
        {
            return true;
        }

        count = 0;
        return false;
    }

    private static MetadataBuilder CreateMetadata()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("test"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        return metadata;
    }
}

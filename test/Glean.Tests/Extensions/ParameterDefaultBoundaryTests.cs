using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;

namespace Glean.Tests.Extensions;

public sealed class ParameterDefaultBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoConstant_NullReader_ReturnsFalseAndDefault(bool markedDefault)
    {
        var metadata = Create(markedDefault, addConstant: false, out var handle, out _);
        using var provider = Serialize(metadata);
        var parameter = provider.GetMetadataReader().GetParameter(handle);
        Assert.False(parameter.TryGetDefaultValue<int>(null!, out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void Constant_NullReader_ThrowsWithoutChangingOutput()
    {
        var metadata = Create(true, true, out var handle, out _);
        using var provider = Serialize(metadata);
        var parameter = provider.GetMetadataReader().GetParameter(handle);
        int value = 17;
        Assert.Throws<NullReferenceException>(() => parameter.TryGetDefaultValue(null!, out value));
        Assert.Equal(17, value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidValueHeap_ThrowsBeforeTypeMismatchAndPreservesOutput(bool wrongType)
    {
        using var provider = InvalidValue(invalidHeap: true);
        var reader = provider.GetMetadataReader();
        var parameter = reader.GetParameter(MetadataTokens.ParameterHandle(1));
        int value = 17;
        if (wrongType)
        {
            bool boolean = true;
            Assert.Throws<BadImageFormatException>(() => parameter.TryGetDefaultValue(reader, out boolean));
            Assert.True(boolean);
        }
        else
        {
            Assert.Throws<BadImageFormatException>(() => parameter.TryGetDefaultValue(reader, out value));
            Assert.Equal(17, value);
        }
    }

    [Fact]
    public void TruncatedValue_MatchingTypeThrowsButMismatchedTypeReturnsFalse()
    {
        using var provider = InvalidValue(invalidHeap: false);
        var reader = provider.GetMetadataReader();
        var parameter = reader.GetParameter(MetadataTokens.ParameterHandle(1));
        int value = 17;
        Assert.Throws<BadImageFormatException>(() => parameter.TryGetDefaultValue(reader, out value));
        Assert.Equal(17, value);
        Assert.False(parameter.TryGetDefaultValue<bool>(reader, out bool boolean));
        Assert.False(boolean);
        Assert.False(parameter.TryGetDefaultValue<DateTime>(reader, out var unsupported));
        Assert.Equal(default, unsupported);
    }

    private static MetadataBuilder Create(bool markedDefault, bool addConstant, out ParameterHandle handle, out BlobHandle truncated)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("defaults"), default, default, default);
        handle = metadata.AddParameter(markedDefault ? ParameterAttributes.HasDefault : 0, default, 1);
        if (addConstant)
        {
            metadata.AddConstant(handle, 5);
        }
        truncated = metadata.GetOrAddBlob(new byte[] { 1 });
        return metadata;
    }

    private static MetadataReaderProvider InvalidValue(bool invalidHeap)
    {
        var metadata = Create(true, true, out _, out var truncated);
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        byte[] bytes = image.ToArray();
        using (var valid = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes)))
        {
            var reader = valid.GetMetadataReader();
            Assert.Equal(6, reader.GetTableRowSize(TableIndex.Constant));
            int offset = reader.GetTableMetadataOffset(TableIndex.Constant) + 4;
            int heap = invalidHeap ? 0xffff : MetadataTokens.GetHeapOffset(truncated);
            bytes[offset] = (byte)heap;
            bytes[offset + 1] = (byte)(heap >> 8);
        }
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes));
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
    }
}

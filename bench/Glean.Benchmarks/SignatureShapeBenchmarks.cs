using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.SignatureShapes")]
public class SignatureShapeBenchmarks : IDisposable
{
    private MetadataReaderProvider _provider = null!;
    private MetadataReader _reader = null!;
    private BlobHandle[] _handles = null!;

    [Params(false, true)]
    public bool GenericInstance { get; set; }

    [Params(false, true)]
    public bool Malformed { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("signatures"), default, default, default);
        byte[][] blobs = GenericInstance
            ? new[] { new byte[] { 0x15, 0x12, 0x04, 0x01 }, new byte[] { 0x15, 0x11, 0x04, 0x80, 0x80 }, new byte[] { 0x15, 0x12, 0x04, 0xc0, 0x00, 0x40, 0x00 } }
            : new[] { new byte[] { 0x08, 0x00 }, new byte[] { 0x28, 0x01 }, new byte[] { 0x08, 0x80, 0x80 }, new byte[] { 0x08, 0xc0, 0x00, 0x40, 0x00 } };
        var handles = blobs.Select(metadata.GetOrAddBlob).ToList();
        if (Malformed)
        {
            handles = new List<BlobHandle> { default, metadata.GetOrAddBlob(new byte[] { 0x15 }),
                metadata.GetOrAddBlob(new byte[] { 0x08, 0xff }), metadata.GetOrAddBlob(new byte[] { 0x15, 0x12, 0x04, 0x80 }),
                MetadataTokens.BlobHandle(0x00ffffff) };
        }
        _handles = handles.ToArray();
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        _provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
        _reader = _provider.GetMetadataReader();
        foreach (var handle in _handles)
        {
            bool raw = ReadRaw(handle, out int expected);
            bool wrapped = ReadGlean(handle, out int actual);
            MetadataFixture.Require((raw == wrapped) && (expected == actual), "Signature result and out-count equivalence");
            if (!Malformed)
            {
                MetadataFixture.Require(raw, "Valid signature input");
            }
        }
        MetadataFixture.Require(Srm() == Glean(), "Signature checksum");
    }

    [Benchmark(Baseline = true)]
    public int Srm()
    {
        int sum = 0;
        foreach (var handle in _handles)
        {
            bool success = ReadRaw(handle, out int count);
            sum += count + (success ? 1 : -1);
        }
        return sum;
    }

    [Benchmark]
    public int Glean()
    {
        int sum = 0;
        foreach (var handle in _handles)
        {
            bool success = ReadGlean(handle, out int count);
            sum += count + (success ? 1 : -1);
        }
        return sum;
    }

    private bool ReadGlean(BlobHandle handle, out int count)
        => GenericInstance ? SignatureInspector.TryGetGenericInstanceArity(_reader, handle, out count)
            : SignatureInspector.TryGetPropertyParameterCount(_reader, handle, out count);

    private bool ReadRaw(BlobHandle handle, out int count)
    {
        count = 0;
        if (handle.IsNil)
        {
            return false;
        }
        try
        {
            var blob = _reader.GetBlobReader(handle);
            if ((GenericInstance) && ((SignatureTypeCode)blob.ReadByte() != SignatureTypeCode.GenericTypeInstance))
            {
                return false;
            }
            if (GenericInstance)
            {
                blob.ReadByte();
                blob.ReadCompressedInteger();
            }
            else
            {
                blob.ReadByte();
            }
            count = blob.ReadCompressedInteger();
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    public void Dispose() => _provider.Dispose();
}

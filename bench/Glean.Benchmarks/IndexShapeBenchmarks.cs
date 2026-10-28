using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Resolution;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.IndexShapes")]
public class IndexShapeBenchmarks : IDisposable
{
    private MetadataReaderProvider _provider = null!;
    private MetadataReader _reader = null!;
    private MemberResolutionIndex _warm = null!;
    private string[] _names = null!;

    [Params(1, 32)]
    public int Names { get; set; }

    [Params(1, 64)]
    public int Overloads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("index-shapes"), default, default, default);
        var methodSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 });
        var fieldSignature = metadata.GetOrAddBlob(new byte[] { 0x06, 0x08 });
        _names = Enumerable.Range(0, Names).Select(i => "Member" + i).ToArray();
        int row = 1;
        for (int type = 0; type < 16; type++)
        {
            metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Owner" + type),
                default, MetadataTokens.FieldDefinitionHandle(row), MetadataTokens.MethodDefinitionHandle(row));
            foreach (string name in _names)
            {
                for (int overload = 0; overload < Overloads; overload++)
                {
                    metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.Runtime,
                        metadata.GetOrAddString(name), methodSignature, 0, MetadataTokens.ParameterHandle(1));
                    metadata.AddFieldDefinition(FieldAttributes.Public, metadata.GetOrAddString(name), fieldSignature);
                    row++;
                }
            }
        }
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        _provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
        _reader = _provider.GetMetadataReader();
        _warm = new MemberResolutionIndex();
        foreach (var type in _reader.TypeDefinitions)
        {
            foreach (string name in _names)
            {
                var expectedMethods = _reader.GetTypeDefinition(type).GetMethods()
                    .Where(h => _reader.StringComparer.Equals(_reader.GetMethodDefinition(h).Name, name)).ToArray();
                var expectedFields = _reader.GetTypeDefinition(type).GetFields()
                    .Where(h => _reader.StringComparer.Equals(_reader.GetFieldDefinition(h).Name, name)).ToArray();
                MetadataFixture.Require(_warm.GetMethodCandidatesWithShapes(_reader, type, name).Select(c => c.Handle).SequenceEqual(expectedMethods), "Index method identities and order");
                MetadataFixture.Require(_warm.GetFieldCandidates(_reader, type, name).SequenceEqual(expectedFields), "Index field identities and order");
            }
            MetadataFixture.Require(_warm.GetMethodCandidatesWithShapes(_reader, type, "missing").Length == 0, "Index absent name");
        }
        long expected = 2L * 16 * Names * Overloads;
        MetadataFixture.Require((WarmCandidates() == expected) && (BuildCandidates() == expected) && (BuildAndProjectHandles() == expected), "Index shape counts");
    }

    [Benchmark]
    public long BuildCandidates() => Read(new MemberResolutionIndex(), projectHandles: false);

    [Benchmark]
    public long BuildAndProjectHandles() => Read(new MemberResolutionIndex(), projectHandles: true);

    [Benchmark]
    public long WarmCandidates() => Read(_warm, projectHandles: false);

    private long Read(MemberResolutionIndex index, bool projectHandles)
    {
        long sum = 0;
        foreach (var type in _reader.TypeDefinitions)
        {
            foreach (string name in _names)
            {
                sum += projectHandles ? index.GetMethodCandidates(_reader, type, name).Length
                    : index.GetMethodCandidatesWithShapes(_reader, type, name).Length;
                sum += index.GetFieldCandidates(_reader, type, name).Length;
            }
        }
        return sum;
    }

    public void Dispose() => _provider.Dispose();
}

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Contexts;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DebugMetadataBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;
    private MetadataReaderProvider _provider = null!;
    private MetadataReader _pdb = null!;

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture("Fixture");
        _provider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(Path.ChangeExtension(_corpus.Path, ".pdb")));
        _pdb = _provider.GetMetadataReader();
        MetadataFixture.Require((SrmSequencePoints() > 0) && (SrmSequencePoints() == GleanSequencePoints()), "Sequence point equivalence");
        MetadataFixture.Require((_pdb.Documents.Count > 0) && (SrmDocuments() == GleanDocuments()), "Document equivalence");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.SequencePoints")]
    public long SrmSequencePoints()
    {
        long sum = 0;
        foreach (var handle in _corpus.Reader.MethodDefinitions)
        {
            sum += _corpus.Reader.GetMethodDefinition(handle).RelativeVirtualAddress;
            foreach (var point in _pdb.GetMethodDebugInformation(handle).GetSequencePoints())
            {
                sum += point.Offset + point.StartLine + MetadataTokens.GetRowNumber(point.Document);
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.SequencePoints")]
    public long GleanSequencePoints()
    {
        long sum = 0;
        foreach (var handle in _corpus.Reader.MethodDefinitions)
        {
            var method = MethodContext.Create(_corpus.Reader, handle);
            sum += method.RelativeVirtualAddress;
            foreach (var point in method.EnumerateSequencePoints(_pdb))
            {
                sum += point.Offset + point.StartLine + MetadataTokens.GetRowNumber(point.Document);
            }
        }
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.Documents")]
    public long SrmDocuments()
    {
        long sum = 0;
        foreach (var handle in _pdb.Documents)
        {
            var document = _pdb.GetDocument(handle);
            sum += MetadataTokens.GetHeapOffset(document.Hash) + _pdb.GetGuid(document.Language).GetHashCode();
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.Documents")]
    public long GleanDocuments()
    {
        long sum = 0;
        foreach (var handle in _pdb.Documents)
        {
            var document = DocumentContext.Create(_pdb, handle);
            sum += MetadataTokens.GetHeapOffset(document.Hash) + document.Language.GetHashCode();
        }
        return sum;
    }

    public void Dispose()
    {
        _provider.Dispose();
        _corpus.Dispose();
    }
}

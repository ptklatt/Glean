using BenchmarkDotNet.Attributes;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.FullNames")]
public class RichFormattingBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        var reader = _corpus.Reader;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            var definition = reader.GetTypeDefinition(type.Handle);
            string ns = reader.GetString(definition.Namespace);
            string name = reader.GetString(definition.Name);
            MetadataFixture.Require(type.FullName == (ns.Length == 0 ? name : string.Concat(ns, ".", name)), "Full name value");
        }
        MetadataFixture.Require(Srm() == Glean(), "Full name checksum");
        Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256}");
    }

    [Benchmark(Baseline = true)]
    public long Srm()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            string ns = reader.GetString(definition.Namespace);
            string name = reader.GetString(definition.Name);
            string fullName = ns.Length == 0 ? name : string.Concat(ns, ".", name);
            sum += fullName.Length;
        }
        return sum;
    }

    [Benchmark]
    public long Glean()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            sum += type.FullName.Length;
        }
        return sum;
    }

    public void Dispose() => _corpus.Dispose();
}

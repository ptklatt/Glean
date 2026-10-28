using System.Reflection.Metadata;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Contexts;
using Glean.Decoding;
using Glean.Providers;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AttributeDecodingBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        int failures = 0;
        foreach (var handle in _corpus.Reader.CustomAttributes)
        {
            MetadataFixture.Require(RawOne(handle) == WrappedOne(handle), "Attribute decode outcome");
            MetadataFixture.Require(RawOne(handle) == MaterializedOne(handle), "Materialized argument counts/failure outcome");
            failures += RawOne(handle) < 0 ? 1 : 0;
        }
        MetadataFixture.Require(SrmValues() == ContextValues(), "Attribute decode checksum");
        long decoded = Materialized();
        MetadataFixture.Require(decoded == Materialized(), "Materialized attribute outcomes");
        Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256} Attributes={_corpus.Reader.CustomAttributes.Count} DecodeFailures={failures}");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.AttributeValues")]
    public long SrmValues()
    {
        long sum = 0;
        foreach (var handle in _corpus.Reader.CustomAttributes)
        {
            sum += RawOne(handle);
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.AttributeValues")]
    public long ContextValues()
    {
        long sum = 0;
        foreach (var handle in _corpus.Reader.CustomAttributes)
        {
            sum += WrappedOne(handle);
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.MaterializedAttributes")]
    public long Materialized()
    {
        long sum = 0;
        foreach (var handle in _corpus.Reader.CustomAttributes)
        {
            sum += MaterializedOne(handle);
        }
        return sum;
    }

    private long MaterializedOne(CustomAttributeHandle handle)
    {
        try
        {
            var value = CustomAttributeDecoder.Decode(_corpus.Reader, handle);
            return value.FixedArguments.Length + value.NamedArguments.Length * 1000L + 1;
        }
        catch (BadImageFormatException)
        {
            return -1;
        }
    }

    private long RawOne(CustomAttributeHandle handle)
    {
        try
        {
            var value = _corpus.Reader.GetCustomAttribute(handle).DecodeValue(new CustomAttributeTypeProvider(_corpus.Reader));
            return value.FixedArguments.Length + value.NamedArguments.Length * 1000L + 1;
        }
        catch (BadImageFormatException)
        {
            return -1;
        }
    }

    private long WrappedOne(CustomAttributeHandle handle)
    {
        try
        {
            var value = CustomAttributeContext.Create(_corpus.Reader, handle).DecodeValue(new CustomAttributeTypeProvider(_corpus.Reader));
            return value.FixedArguments.Length + value.NamedArguments.Length * 1000L + 1;
        }
        catch (BadImageFormatException)
        {
            return -1;
        }
    }

    public void Dispose() => _corpus.Dispose();
}

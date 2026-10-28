using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Contexts;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.Accessors")]
public class FocusedAccessorBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        MetadataFixture.Require(RawAccessors() == ContextAccessors(), "Accessor convenience results");
        MetadataFixture.Require(RawAccessors() == GroupedAccessors(), "Grouped accessor results");
        Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256}");
    }

    [Benchmark(Baseline = true)]
    public long RawAccessors()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            foreach (var property in type.GetProperties())
            {
                var value = reader.GetPropertyDefinition(property).GetAccessors();
                sum += ReadAccessor(reader, value.Getter) + ReadAccessor(reader, value.Setter);
            }
            foreach (var eventHandle in type.GetEvents())
            {
                var value = reader.GetEventDefinition(eventHandle).GetAccessors();
                sum += ReadAccessor(reader, value.Adder) + ReadAccessor(reader, value.Remover) + ReadAccessor(reader, value.Raiser);
            }
        }
        return sum;
    }

    [Benchmark]
    public long ContextAccessors()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            foreach (var property in type.EnumerateProperties())
            {
                sum += ReadAccessor(property.Getter) + ReadAccessor(property.Setter);
            }
            foreach (var value in type.EnumerateEvents())
            {
                sum += ReadAccessor(value.AddMethod) + ReadAccessor(value.RemoveMethod) + ReadAccessor(value.RaiseMethod);
            }
        }
        return sum;
    }

    [Benchmark]
    public long GroupedAccessors()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            foreach (var property in type.EnumerateProperties())
            {
                var value = property.Definition.GetAccessors();
                sum += ReadContext(reader, value.Getter) + ReadContext(reader, value.Setter);
            }
            foreach (var eventContext in type.EnumerateEvents())
            {
                var value = eventContext.Definition.GetAccessors();
                sum += ReadContext(reader, value.Adder) + ReadContext(reader, value.Remover) + ReadContext(reader, value.Raiser);
            }
        }
        return sum;
    }

    private static long ReadAccessor(MetadataReader reader, MethodDefinitionHandle handle)
        => handle.IsNil ? 0 : MetadataTokens.GetRowNumber(handle) + (int)reader.GetMethodDefinition(handle).Attributes;

    private static long ReadAccessor(MethodContext method)
        => method.IsValid ? MetadataTokens.GetRowNumber(method.Handle) + (int)method.Attributes : 0;

    private static long ReadContext(MetadataReader reader, MethodDefinitionHandle handle)
    {
        if (handle.IsNil)
        {
            return 0;
        }
        var method = MethodContext.Create(reader, handle);
        return MetadataTokens.GetRowNumber(method.Handle) + (int)method.Attributes;
    }

    public void Dispose() => _corpus.Dispose();
}

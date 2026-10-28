using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Extensions;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SemanticInspectionBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        MetadataFixture.Require(DefinitionTypeChecks() == ContextTypeChecks(), "Semantic type checks");
        MetadataFixture.Require(SrmConstructors() == GleanConstructors(), "Constructor checks");
        MetadataFixture.Require(DefinitionAttributeChecks() == ContextAttributeChecks(), "Attribute presence checks");
        Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256}");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.TypeClassification")]
    public long DefinitionTypeChecks()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            sum += definition.IsValueType(reader) ? 1 : 0;
            sum += definition.IsEnum(reader) ? 2 : 0;
            sum += definition.IsDelegate(reader) ? 4 : 0;
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.TypeClassification")]
    public long ContextTypeChecks()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            sum += type.IsValueType ? 1 : 0;
            sum += type.IsEnum ? 2 : 0;
            sum += type.IsDelegate ? 4 : 0;
        }
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.Constructors")]
    public long SrmConstructors()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var type in reader.TypeDefinitions)
        {
            foreach (var handle in reader.GetTypeDefinition(type).GetMethods())
            {
                var method = reader.GetMethodDefinition(handle);
                if (((method.Attributes & System.Reflection.MethodAttributes.RTSpecialName) != 0) &&
                    (reader.StringComparer.Equals(method.Name, ".ctor") || reader.StringComparer.Equals(method.Name, ".cctor")))
                {
                    sum += MetadataTokens.GetRowNumber(handle);
                }
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.Constructors")]
    public long GleanConstructors()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            foreach (var method in type.EnumerateMethods())
            {
                if (method.IsConstructor)
                {
                    sum += MetadataTokens.GetRowNumber(method.Handle);
                }
            }
        }
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.AttributePresence")]
    public long DefinitionAttributeChecks()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            sum += type.GetCustomAttributes().TryFindAttribute(reader, "Glean.Benchmarks.Fixture", "SampleAttribute", out _) ? 1 : 0;
            sum += type.GetCustomAttributes().TryFindAttribute(reader, "System.Runtime.CompilerServices", "CompilerGeneratedAttribute", out _) ? 2 : 0;
            sum += type.GetCustomAttributes().TryFindAttribute(reader, "Missing", "MissingAttribute", out _) ? 4 : 0;
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                sum += method.GetCustomAttributes().TryFindAttribute(reader, "Glean.Benchmarks.Fixture", "SampleAttribute", out _) ? 8 : 0;
                sum += method.GetCustomAttributes().TryFindAttribute(reader, "System.Runtime.CompilerServices", "CompilerGeneratedAttribute", out _) ? 16 : 0;
                sum += method.GetCustomAttributes().TryFindAttribute(reader, "Missing", "MissingAttribute", out _) ? 32 : 0;
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.AttributePresence")]
    public long ContextAttributeChecks()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            sum += type.HasAttribute("Glean.Benchmarks.Fixture", "SampleAttribute") ? 1 : 0;
            sum += type.HasAttribute("System.Runtime.CompilerServices", "CompilerGeneratedAttribute") ? 2 : 0;
            sum += type.HasAttribute("Missing", "MissingAttribute") ? 4 : 0;
            foreach (var method in type.EnumerateMethods())
            {
                sum += method.HasAttribute("Glean.Benchmarks.Fixture", "SampleAttribute") ? 8 : 0;
                sum += method.HasAttribute("System.Runtime.CompilerServices", "CompilerGeneratedAttribute") ? 16 : 0;
                sum += method.HasAttribute("Missing", "MissingAttribute") ? 32 : 0;
            }
        }
        return sum;
    }

    public void Dispose() => _corpus.Dispose();
}

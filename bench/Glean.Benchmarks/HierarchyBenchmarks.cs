using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Extensions;
using Glean.Resolution;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HierarchyBenchmarks : IDisposable
{
    private ResolutionCorpus _corpus = null!;
    private AssemblySet _set = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new ResolutionCorpus(Corpus);
        _set = _corpus.CreateSet();
        foreach (var type in _corpus.Source.Assembly.EnumerateTypes())
        {
            var seen = new HashSet<(MetadataReader, TypeDefinitionHandle)>();
            foreach (var implementation in type.EnumerateAllInterfaces(_set))
            {
                MetadataFixture.Require(seen.Add((implementation.Reader, implementation.Handle)), "Duplicate interface result");
            }
        }
        MetadataFixture.Require(RawLocalBases() == LocalBases(), "Local base traversal");
        long resolved = ResolvedBases();
        long interfaces = Interfaces();
        MetadataFixture.Require((resolved == ResolvedBases()) && (interfaces == Interfaces()), "Repeated resolved hierarchy outcomes");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.LocalHierarchy")]
    public long RawLocalBases()
    {
        var reader = _corpus.Source.Reader;
        long sum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var current = reader.GetTypeDefinition(handle).BaseType;
            var checkpoint = handle;
            long power = 1;
            long length = 0;
            while ((!current.IsNil) && (current.Kind == HandleKind.TypeDefinition))
            {
                var parent = (TypeDefinitionHandle)current;
                var definition = reader.GetTypeDefinition(parent);
                if ((parent == handle) || (parent == checkpoint))
                {
                    break;
                }
                sum += MetadataTokens.GetRowNumber(parent);
                current = definition.BaseType;
                length++;
                if (length == power)
                {
                    checkpoint = parent;
                    power *= 2;
                    length = 0;
                }
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.LocalHierarchy")]
    public long LocalBases()
    {
        long sum = 0;
        foreach (var type in _corpus.Source.Assembly.EnumerateTypes())
        {
            foreach (var parent in type.EnumerateBaseTypes())
            {
                sum += MetadataTokens.GetRowNumber(parent.Handle);
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.Hierarchy")]
    public long ResolvedBases()
    {
        long sum = 0;
        foreach (var type in _corpus.Source.Assembly.EnumerateTypes())
        {
            foreach (var parent in type.EnumerateBaseTypes(_set))
            {
                sum += MetadataTokens.GetRowNumber(parent.Handle) + 0x100000000L * _corpus.ReaderIdentity(parent.Reader);
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.Hierarchy")]
    public long Interfaces()
    {
        long sum = 0;
        foreach (var type in _corpus.Source.Assembly.EnumerateTypes())
        {
            foreach (var implementation in type.EnumerateAllInterfaces(_set))
            {
                sum += MetadataTokens.GetRowNumber(implementation.Handle) + 0x100000000L * _corpus.ReaderIdentity(implementation.Reader);
            }
        }
        return sum;
    }

    public void Dispose()
    {
        _set.Dispose();
        _corpus.Dispose();
    }
}

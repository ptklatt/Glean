using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LoadingBenchmarks
{
    private string _path = "";
    private string[] _searchPaths = null!;

    [Params("Fixture", "Linq")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        using var corpus = new CorpusFixture(Corpus);
        _path = corpus.Path;
        _searchPaths = new[] { Path.GetDirectoryName(_path)!, CorpusFixture.RuntimeDirectory };
        MetadataFixture.Require(SrmOpen() == ScopeOpen(), "File-open metadata outputs");
        MetadataFixture.Require(ClosureLoad() == ClosureLoad(), "Repeated closure outcome");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.FileOpen")]
    public int SrmOpen()
    {
        using var pe = new PEReader(File.OpenRead(_path));
        var reader = pe.GetMetadataReader();
        return reader.TypeDefinitions.Count + reader.GetString(reader.GetAssemblyDefinition().Name).Length;
    }

    [Benchmark, BenchmarkCategory("Spectrum.FileOpen")]
    public int ScopeOpen()
    {
        using var scope = AssemblyScope.Open(_path);
        return scope.Reader.TypeDefinitions.Count + scope.Context.Name.Length;
    }

    [Benchmark, BenchmarkCategory("Spectrum.ClosureLoad")]
    public int ClosureLoad()
    {
        using var closure = AssemblyClosure.Load(_path, _searchPaths);
        return closure.Set.RegisteredAssemblies.Count + 1000 * closure.SkippedDependencies.Count;
    }
}

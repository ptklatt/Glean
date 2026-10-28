using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Resolution;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.BindingMisses")]
public class BindingMissBenchmarks : IDisposable
{
    private MetadataReaderProvider _provider = null!;
    private MetadataReader _reader = null!;
    private AssemblySet _set = null!;
    private readonly MissResolver _resolver = new();

    [Params(false, true)]
    public bool WithResolver { get; set; }

    [Params(AssemblyBindingPolicy.Exact, AssemblyBindingPolicy.ExactThenCompatible)]
    public AssemblyBindingPolicy Policy { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("missing-binding"), default, default, default);
        var reference = metadata.AddAssemblyReference(metadata.GetOrAddString("MissingDependency"), new Version(1, 0, 0, 0), default, default, 0, default);
        metadata.AddTypeReference(reference, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Absent"));
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        _provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
        _reader = _provider.GetMetadataReader();
        _set = new AssemblySet(Policy, WithResolver ? _resolver : null);
        long before = _resolver.Calls;
        MetadataFixture.Require(MissingAssembly() == (int)ResolutionFailureReason.AssemblyNotFound, "Missing assembly failure");
        MetadataFixture.Require(MissingAssembly() == (int)ResolutionFailureReason.AssemblyNotFound, "Repeated missing assembly failure");
        MetadataFixture.Require(_resolver.Calls - before == (WithResolver ? 2 : 0), "Resolver retries remain observable");
    }

    [Benchmark]
    public int MissingAssembly()
    {
        bool success = _set.TryResolveType(_reader, MetadataTokens.TypeReferenceHandle(1), out _, out _, out var reason);
        return success ? -1 : (int)reason;
    }

    public void Dispose()
    {
        _set.Dispose();
        _provider.Dispose();
    }

    private sealed class MissResolver : IAssemblyReferenceResolver
    {
        public long Calls;
        public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader reader, out MetadataReader resolved)
        {
            Calls++;
            resolved = null!;
            return false;
        }
    }
}

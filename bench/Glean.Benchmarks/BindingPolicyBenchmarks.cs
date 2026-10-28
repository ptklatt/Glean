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
[BenchmarkCategory("Spectrum.BindingPolicy")]
public class BindingPolicyBenchmarks : IDisposable
{
    private MetadataReaderProvider _source = null!;
    private MetadataReaderProvider _target = null!;
    private MetadataReader _reader = null!;
    private MetadataReader _targetReader = null!;
    private AssemblySet _set = null!;

    [Params(AssemblyBindingPolicy.Exact, AssemblyBindingPolicy.ExactThenCompatible)]
    public AssemblyBindingPolicy Policy { get; set; }

    [Params(false, true)]
    public bool FallbackOnly { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var source = new MetadataBuilder();
        source.AddModule(0, source.GetOrAddString("requester"), default, default, default);
        var reference = source.AddAssemblyReference(source.GetOrAddString("Dependency"), new Version(1, 0, 0, 0), default, default, 0, default);
        source.AddTypeReference(reference, source.GetOrAddString("Fixture"), source.GetOrAddString("Target"));
        _source = Serialize(source);
        _reader = _source.GetMetadataReader();
        var target = new MetadataBuilder();
        target.AddModule(0, target.GetOrAddString("dependency"), default, default, default);
        target.AddAssembly(target.GetOrAddString("Dependency"), new Version(FallbackOnly ? 2 : 1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        target.AddTypeDefinition(TypeAttributes.Public, target.GetOrAddString("Fixture"), target.GetOrAddString("Target"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        _target = Serialize(target);
        _targetReader = _target.GetMetadataReader();
        _set = new AssemblySet(Policy);
        _set.Add(_targetReader);
        bool success = _set.TryResolveType(_reader, MetadataTokens.TypeReferenceHandle(1), out var actual, out var handle, out var reason);
        bool expected = !FallbackOnly || (Policy == AssemblyBindingPolicy.ExactThenCompatible);
        MetadataFixture.Require(success == expected, "Binding policy outcome");
        if (expected)
        {
            MetadataFixture.Require(ReferenceEquals(actual, _targetReader) && (handle == MetadataTokens.TypeDefinitionHandle(1)), "Binding policy reader/handle");
        }
        else
        {
            MetadataFixture.Require(reason == ResolutionFailureReason.AssemblyNotFound, "Exact version mismatch");
        }
        MetadataFixture.Require(ResolveRegistered() == ResolveRegistered(), "Repeated policy outcome");
    }

    [Benchmark]
    public int ResolveRegistered()
    {
        bool success = _set.TryResolveType(_reader, MetadataTokens.TypeReferenceHandle(1), out _, out var handle, out var reason);
        return success ? MetadataTokens.GetToken(handle) : -(int)reason - 1;
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
    }

    public void Dispose()
    {
        _set.Dispose();
        _target.Dispose();
        _source.Dispose();
    }
}

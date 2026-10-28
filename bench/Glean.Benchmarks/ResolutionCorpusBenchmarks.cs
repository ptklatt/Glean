using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Resolution;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.Resolution")]
public class ResolutionCorpusBenchmarks : IDisposable
{
    private ResolutionCorpus _corpus = null!;
    private AssemblySet _warm = null!;
    private MemberResolutionIndex _index = null!;
    private MemberReferenceHandle[] _all = null!;
    private MemberReferenceHandle[] _hits = null!;
    private MemberReferenceHandle[] _misses = null!;

    [Params("Fixture", "Linq")]
    public string Corpus { get; set; } = "Fixture";

    [Params(false, true)]
    public bool CacheSignatures { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new ResolutionCorpus(Corpus);
        _warm = _corpus.CreateSet();
        _index = new MemberResolutionIndex(CacheSignatures);
        _all = _corpus.Source.Reader.MemberReferences.ToArray();
        var hits = new List<MemberReferenceHandle>();
        var misses = new List<MemberReferenceHandle>();
        using var uncached = _corpus.CreateSet();
        foreach (var reference in _all)
        {
            bool expected = uncached.TryResolveMember(_corpus.Source.Reader, reference, out var leftReader, out var leftHandle, out var leftReason);
            bool actual = _warm.TryResolveMember(_corpus.Source.Reader, reference, _index, out var rightReader, out var rightHandle, out var rightReason);
            MetadataFixture.Require((expected == actual) && ReferenceEquals(leftReader, rightReader) &&
                (leftHandle == rightHandle) && (leftReason == rightReason), $"Indexed resolution target/failure {MetadataTokens.GetToken(reference):X8}");
            (actual ? hits : misses).Add(reference);
        }
        _hits = hits.ToArray();
        _misses = misses.ToArray();
        long expectedSum = Resolve(_warm, _index, _all);
        MetadataFixture.Require(ColdMembers() == expectedSum, "Cold real member outcomes");
        MetadataFixture.Require(RefreshedMembers() == expectedSum, "Refreshed real member outcomes");
        MetadataFixture.Require(RebuiltIndexMembers() == expectedSum, "Rebuilt real member outcomes");
        Console.WriteLine($"Corpus={_corpus.Source.Path} SHA256={_corpus.Source.Sha256} Hits={_hits.Length} Misses={_misses.Length} CacheSignatures={CacheSignatures}");
    }

    [Benchmark]
    public long ColdMembers()
    {
        using var set = _corpus.CreateSet();
        return Resolve(set, new MemberResolutionIndex(CacheSignatures), _all);
    }

    [Benchmark]
    public long WarmMemberHits() => Resolve(_warm, _index, _hits);

    [Benchmark]
    public long WarmMemberMisses() => Resolve(_warm, _index, _misses);

    [Benchmark]
    public long WarmMixedMembers() => Resolve(_warm, _index, _all);

    [Benchmark]
    public long RefreshedMembers()
    {
        _warm.Add(_corpus.Source.Reader);
        return Resolve(_warm, _index, _all);
    }

    [Benchmark]
    public long RebuiltIndexMembers()
    {
        _index.Clear();
        _warm.Add(_corpus.Source.Reader);
        return Resolve(_warm, _index, _all);
    }

    private long Resolve(AssemblySet set, MemberResolutionIndex index, MemberReferenceHandle[] references)
    {
        long sum = 0;
        foreach (var reference in references)
        {
            bool success = set.TryResolveMember(_corpus.Source.Reader, reference, index, out var reader, out var handle, out var reason);
            long outcome = success ? MetadataTokens.GetToken(handle) + 0x100000000L * _corpus.ReaderIdentity(reader) : -(long)reason - 1;
            sum = unchecked((sum * 31 + MetadataTokens.GetToken(reference)) * 31 + outcome);
        }
        return sum;
    }

    public void Dispose()
    {
        _warm.Dispose();
        _corpus.Dispose();
    }
}

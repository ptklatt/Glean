using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Resolution;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[JsonExporterAttribute.Full]
public class ResolutionBenchmarks : IDisposable
{
    private MetadataFixture _fixture = null!;
    private AssemblySet _warmSet = null!;
    private MemberResolutionIndex _warmMembers = null!;
    private SrmResolutionIndex _warmSrm = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MetadataFixture();
        _warmSet = CreateSet();
        _warmMembers = new MemberResolutionIndex(cacheSemanticSignatures: false);
        _warmSrm = new SrmResolutionIndex(_fixture.Reader);
        ValidateTargets();
        int expectedTypes = Enumerable.Range(2, MetadataFixture.TypeCount).Sum();
        int expectedMembers = Enumerable.Range(0, MetadataFixture.TypeCount)
            .Sum(i => 2 * i * MetadataFixture.MethodsPerType + 3);
        MetadataFixture.Require(SrmWarmTypes() == expectedTypes, "Warm type checksum");
        MetadataFixture.Require(GleanWarmTypes() == expectedTypes, "Glean warm type checksum");
        MetadataFixture.Require(SrmColdTypes() == expectedTypes, "Cold type checksum");
        MetadataFixture.Require(GleanColdTypes() == expectedTypes, "Glean cold type checksum");
        MetadataFixture.Require(SrmWarmMembers() == expectedMembers, "Warm member checksum");
        MetadataFixture.Require(GleanWarmMembers() == expectedMembers, "Glean warm member checksum");
        MetadataFixture.Require(SrmColdMembers() == expectedMembers, "Cold member checksum");
        MetadataFixture.Require(GleanColdMembers() == expectedMembers, "Glean cold member checksum");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Resolution.WarmTypes")]
    public int SrmWarmTypes() => ResolveSrmTypes(_warmSrm);

    [Benchmark, BenchmarkCategory("Resolution.WarmTypes")]
    public int GleanWarmTypes() => ResolveGleanTypes(_warmSet);

    [Benchmark(Baseline = true), BenchmarkCategory("Resolution.WarmMembers")]
    public int SrmWarmMembers() => ResolveSrmMembers(_warmSrm);

    [Benchmark, BenchmarkCategory("Resolution.WarmMembers")]
    public int GleanWarmMembers() => ResolveGleanMembers(_warmSet, _warmMembers);

    [Benchmark(Baseline = true), BenchmarkCategory("Resolution.ColdTypes")]
    public int SrmColdTypes() => ResolveSrmTypes(new SrmResolutionIndex(_fixture.Reader));

    [Benchmark, BenchmarkCategory("Resolution.ColdTypes")]
    public int GleanColdTypes()
    {
        using var set = CreateSet();
        return ResolveGleanTypes(set);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Resolution.ColdMembers")]
    public int SrmColdMembers() => ResolveSrmMembers(new SrmResolutionIndex(_fixture.Reader));

    [Benchmark, BenchmarkCategory("Resolution.ColdMembers")]
    public int GleanColdMembers()
    {
        using var set = CreateSet();
        return ResolveGleanMembers(set, new MemberResolutionIndex(cacheSemanticSignatures: false));
    }

    private AssemblySet CreateSet()
    {
        var set = new AssemblySet();
        set.Add(_fixture.Reader);
        return set;
    }

    private int ResolveSrmTypes(SrmResolutionIndex index)
    {
        int checksum = 0;
        foreach (var reference in _fixture.TypeReferences)
        {
            if (!index.TryResolveType(_fixture.Source, reference, out var reader, out var handle) ||
                !ReferenceEquals(reader, _fixture.Reader))
            {
                throw new InvalidOperationException("SRM type resolution failed.");
            }

            checksum += MetadataTokens.GetRowNumber(handle);
        }

        return checksum;
    }

    private int ResolveGleanTypes(AssemblySet set)
    {
        int checksum = 0;
        foreach (var reference in _fixture.TypeReferences)
        {
            if (!set.TryResolveType(_fixture.Source, reference, out var reader, out var handle) ||
                !ReferenceEquals(reader, _fixture.Reader))
            {
                throw new InvalidOperationException("Glean type resolution failed.");
            }

            checksum += MetadataTokens.GetRowNumber(handle);
        }

        return checksum;
    }

    private int ResolveSrmMembers(SrmResolutionIndex index)
    {
        int checksum = 0;
        foreach (var reference in _fixture.MemberReferences)
        {
            if (!index.TryResolveMember(_fixture.Source, reference, out var reader, out var handle) ||
                !ReferenceEquals(reader, _fixture.Reader))
            {
                throw new InvalidOperationException("SRM member resolution failed.");
            }

            checksum += MetadataTokens.GetRowNumber(handle);
        }

        return checksum;
    }

    private int ResolveGleanMembers(AssemblySet set, MemberResolutionIndex index)
    {
        int checksum = 0;
        foreach (var reference in _fixture.MemberReferences)
        {
            if (!set.TryResolveMember(_fixture.Source, reference, index, out var reader, out var handle) ||
                !ReferenceEquals(reader, _fixture.Reader))
            {
                throw new InvalidOperationException("Glean member resolution failed.");
            }

            checksum += MetadataTokens.GetRowNumber(handle);
        }

        return checksum;
    }

    private void ValidateTargets()
    {
        using var coldSet = CreateSet();
        var coldMembers = new MemberResolutionIndex(cacheSemanticSignatures: false);
        var coldSrm = new SrmResolutionIndex(_fixture.Reader);
        for (int i = 0; i < _fixture.TypeReferences.Length; i++)
        {
            var reference = _fixture.TypeReferences[i];
            MetadataFixture.Require(_warmSrm.TryResolveType(_fixture.Source, reference, out var srmReader, out var srmType),
                "SRM target type");
            MetadataFixture.Require(_warmSet.TryResolveType(_fixture.Source, reference, out var gleanReader, out var gleanType),
                "Glean target type");
            MetadataFixture.Require(ReferenceEquals(srmReader, gleanReader) && (srmType == gleanType) &&
                (MetadataTokens.GetRowNumber(gleanType) == i + 2), "Target type identity");
        }

        for (int i = 0; i < _fixture.MemberReferences.Length; i++)
        {
            var reference = _fixture.MemberReferences[i];
            MetadataFixture.Require(coldSrm.TryResolveMember(_fixture.Source, reference, out var coldSrmReader,
                out var coldSrmMember), "Cold SRM target member");
            MetadataFixture.Require(coldSet.TryResolveMember(_fixture.Source, reference, coldMembers,
                out var coldGleanReader, out var coldGleanMember), "Cold Glean target member");
            MetadataFixture.Require(_warmSrm.TryResolveMember(_fixture.Source, reference, out var srmReader, out var srmMember),
                "SRM target member");
            MetadataFixture.Require(_warmSet.TryResolveMember(_fixture.Source, reference, _warmMembers,
                out var gleanReader, out var gleanMember), "Glean target member");
            int expectedRow = i / 2 * MetadataFixture.MethodsPerType + i % 2 + 1;
            MetadataFixture.Require(ReferenceEquals(srmReader, gleanReader) && (srmMember == gleanMember) &&
                (gleanMember.Kind == HandleKind.MethodDefinition) &&
                (MetadataTokens.GetRowNumber(gleanMember) == expectedRow), "Target member identity and overload");
            MetadataFixture.Require(ReferenceEquals(coldSrmReader, gleanReader) &&
                ReferenceEquals(coldGleanReader, gleanReader) && (coldSrmMember == gleanMember) &&
                (coldGleanMember == gleanMember), "Cold target member identity and overload");
        }
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _warmSet?.Dispose();
        _fixture?.Dispose();
    }
}

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Tests.Resolution;

public sealed class TypeReferenceCycleTests
{
    public static IEnumerable<object[]> Cycles()
    {
        foreach (var policy in new[] { AssemblyBindingPolicy.Exact, AssemblyBindingPolicy.ExactThenCompatible })
        {
            yield return new object[] { 0, 1, policy };
            yield return new object[] { 0, 2, policy };
            yield return new object[] { 3, 2, policy };
        }
    }

    public static IEnumerable<object[]> NestedChains()
    {
        foreach (var policy in new[] { AssemblyBindingPolicy.Exact, AssemblyBindingPolicy.ExactThenCompatible })
        {
            foreach (int depth in new[] { 1, 64, 4096 })
            {
                yield return new object[] { depth, policy };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cycles))]
    public void TryResolveType_CyclicEnclosingReferences_ThrowsBadImageFormat(
        int prefixLength, int cycleLength, AssemblyBindingPolicy policy)
    {
        using var provider = CreateCycle(prefixLength, cycleLength);
        var reader = provider.GetMetadataReader();
        using var set = new AssemblySet(policy);
        set.Add(reader);

        Assert.Throws<BadImageFormatException>(() =>
            set.TryResolveType(reader, MetadataTokens.TypeReferenceHandle(1), out _, out _));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    public void SignatureComparison_CyclicEnclosingReferences_ThrowsBadImageFormat(int prefixLength, int cycleLength)
    {
        using var provider = CreateCycle(prefixLength, cycleLength);
        var reader = provider.GetMetadataReader();
        var reference = new TypeReferenceSignature(reader, MetadataTokens.TypeReferenceHandle(1));
        var definition = new TypeDefinitionSignature(reader, MetadataTokens.TypeDefinitionHandle(1));
        using var set = new AssemblySet();
        set.Add(reader);

        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(reference, definition));
        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(definition, reference));
        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(reference, definition, set));
        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(definition, reference, set));
    }

    [Theory]
    [MemberData(nameof(NestedChains))]
    public void TryResolveType_DeepNestedReferences_ResolveBeforeAndAfterClearingCaches(int depth, AssemblyBindingPolicy policy)
    {
        using var fixture = new NestedFixture(depth);
        using var set = new AssemblySet(policy);
        set.Add(fixture.Reader);

        for (int pass = 0; pass < 2; pass++)
        {
            Assert.True(set.TryResolveType(fixture.Reader, MetadataTokens.TypeReferenceHandle(depth),
                out var reader, out var handle, out var reason));
            Assert.Same(fixture.Reader, reader);
            Assert.Equal(MetadataTokens.TypeDefinitionHandle(depth), handle);
            Assert.Equal(ResolutionFailureReason.None, reason);

            foreach (int row in new[] { 1, (depth + 1) / 2, depth })
            {
                Assert.True(set.TryResolveType(fixture.Reader, MetadataTokens.TypeReferenceHandle(row), out reader, out handle));
                Assert.Same(fixture.Reader, reader);
                Assert.Equal(MetadataTokens.TypeDefinitionHandle(row), handle);
            }
            set.ClearCaches();
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(4096)]
    public void SignatureComparison_DeepNestedReferences_MatchDefinitions(int depth)
    {
        using var fixture = new NestedFixture(depth);
        var reference = new TypeReferenceSignature(fixture.Reader, MetadataTokens.TypeReferenceHandle(depth));
        var definition = new TypeDefinitionSignature(fixture.Reader, MetadataTokens.TypeDefinitionHandle(depth));
        using var set = new AssemblySet();
        set.Add(fixture.Reader);

        Assert.True(SignatureComparison.TypeSignaturesEquivalent(reference, definition));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(definition, reference));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(reference, definition, set));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(definition, reference, set));
    }

    [Theory]
    [InlineData(false, AssemblyBindingPolicy.Exact)]
    [InlineData(true, AssemblyBindingPolicy.Exact)]
    [InlineData(false, AssemblyBindingPolicy.ExactThenCompatible)]
    [InlineData(true, AssemblyBindingPolicy.ExactThenCompatible)]
    public void TryResolveType_MissingNestedOrOuterName_PreservesFailureReason(bool missingRoot, AssemblyBindingPolicy policy)
    {
        using var fixture = new NestedFixture(3, missingRoot, !missingRoot);
        using var set = new AssemblySet(policy);
        set.Add(fixture.Reader);
        if (!missingRoot)
        {
            Assert.True(set.TryResolveType(fixture.Reader, MetadataTokens.TypeReferenceHandle(2), out _, out _));
        }

        Assert.False(set.TryResolveType(fixture.Reader, MetadataTokens.TypeReferenceHandle(3),
            out var reader, out var handle, out var reason));
        Assert.Null(reader);
        Assert.True(handle.IsNil);
        Assert.Equal(missingRoot ? ResolutionFailureReason.TypeNotFound : ResolutionFailureReason.NestedTypeNotFound, reason);
    }

    [Fact]
    public void TryResolveType_CancelledCyclicReference_ThrowsCancellationFirst()
    {
        using var provider = CreateCycle(0, 1);
        using var set = new AssemblySet();

        Assert.Throws<OperationCanceledException>(() => set.TryResolveType(provider.GetMetadataReader(),
            MetadataTokens.TypeReferenceHandle(1), out _, out _, new CancellationToken(canceled: true)));
    }

    private static MetadataReaderProvider CreateCycle(int prefixLength, int cycleLength)
    {
        var metadata = CreateMetadata();
        metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Target"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        int count = prefixLength + cycleLength;
        for (int i = 0; i < count; i++)
        {
            int next = i == count - 1 ? prefixLength + 1 : i + 2;
            metadata.AddTypeReference(MetadataTokens.TypeReferenceHandle(next), default, metadata.GetOrAddString("T" + i));
        }
        return Serialize(metadata);
    }

    private static MetadataBuilder CreateMetadata()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("NestedFixture"), default, default, default);
        metadata.AddAssembly(metadata.GetOrAddString("NestedFixture"), new Version(1, 0, 0, 0),
            default, default, 0, AssemblyHashAlgorithm.None);
        return metadata;
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var blob = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(blob, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(blob.ToArray()));
    }

    private sealed class NestedFixture : IDisposable
    {
        private readonly MetadataReaderProvider _provider;

        public NestedFixture(int depth, bool missingRoot = false, bool missingLeaf = false)
        {
            var metadata = CreateMetadata();
            EntityHandle scope = MetadataTokens.EntityHandle(1);
            TypeDefinitionHandle parent = default;
            for (int i = 0; i < depth; i++)
            {
                string name = "T" + i;
                string referenceName = ((missingRoot && (i == 0)) || (missingLeaf && (i == depth - 1))) ? "Missing" : name;
                var definition = metadata.AddTypeDefinition(i == 0 ? TypeAttributes.Public : TypeAttributes.NestedPublic,
                    metadata.GetOrAddString(i == 0 ? "Fixture" : "Ignored"), metadata.GetOrAddString(name), default,
                    MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
                if (!parent.IsNil)
                {
                    metadata.AddNestedType(definition, parent);
                }
                scope = metadata.AddTypeReference(scope, metadata.GetOrAddString(i == 0 ? "Fixture" : "Ignored"),
                    metadata.GetOrAddString(referenceName));
                parent = definition;
            }
            _provider = Serialize(metadata);
            Reader = _provider.GetMetadataReader();
        }

        public MetadataReader Reader { get; }

        public void Dispose() => _provider.Dispose();
    }
}

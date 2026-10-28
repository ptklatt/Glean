using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;

namespace Glean.Tests.Resolution;

public sealed class AssemblyBindingBehaviorTests
{
    [Theory]
    [InlineData(AssemblyBindingPolicy.Exact)]
    [InlineData(AssemblyBindingPolicy.ExactThenCompatible)]
    public void FailedBinding_RetriesResolverAndAcceptsLaterMatchingReader(AssemblyBindingPolicy policy)
    {
        using var source = References();
        using var target = Definitions("Dependency");
        using var wrong = Definitions("Different");
        var resolver = new MutableResolver { Target = wrong.GetMetadataReader() };
        using var set = new AssemblySet(policy, resolver);
        var reader = source.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);

        Assert.False(set.TryResolveType(reader, handle, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
        resolver.Target = target.GetMetadataReader();
        Assert.True(set.TryResolveType(reader, handle, out var actualReader, out var actualHandle));
        Assert.Same(resolver.Target, actualReader);
        Assert.Equal(MetadataTokens.TypeDefinitionHandle(1), actualHandle);
        Assert.Equal(2, resolver.Calls);
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
    }

    [Fact]
    public void FailedBinding_ClearThenRegister_ResolvesWithoutStaleFailure()
    {
        using var source = References();
        using var target = Definitions("Dependency");
        using var set = new AssemblySet();
        var targetReader = target.GetMetadataReader();
        var reader = source.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.False(set.TryResolveType(reader, handle, out _, out _));
        Assert.False(set.TryResolveType(reader, handle, out _, out _));
        set.ClearCaches();
        Assert.False(set.TryResolveType(reader, handle, out _, out _));
        set.Add(targetReader);
        Assert.True(set.TryResolveType(reader, handle, out var actual, out _));
        Assert.Same(targetReader, actual);
        set.ClearCaches();
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
    }

    [Fact]
    public void FailedCallback_AddsReaderButReturnsFalse_NextLookupUsesRegistration()
    {
        using var source = References();
        using var target = Definitions("Dependency");
        var resolver = new MutableResolver();
        using var set = new AssemblySet(resolver);
        var targetReader = target.GetMetadataReader();
        resolver.OnAttempt = () => set.Add(targetReader);
        var reader = source.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.False(set.TryResolveType(reader, handle, out _, out _));
        Assert.True(set.TryResolveType(reader, handle, out var actual, out _));
        Assert.Same(targetReader, actual);
        Assert.Equal(1, resolver.Calls);
    }

    private static MetadataReaderProvider References(string name = "Dependency", string typeName = "Target")
    {
        var metadata = Create("Consumer");
        var assembly = metadata.AddAssemblyReference(metadata.GetOrAddString(name), new Version(1, 0, 0, 0), default, default, 0, default);
        metadata.AddTypeReference(assembly, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString(typeName));
        return Serialize(metadata);
    }

    [Fact]
    public void CompatibleFallback_ExactRegistrationTakesPrecedenceOnNextLookup()
    {
        using var source = References();
        using var fallback = Definitions("Dependency", 2);
        using var exact = Definitions("Dependency");
        var sourceReader = source.GetMetadataReader();
        var fallbackReader = fallback.GetMetadataReader();
        var exactReader = exact.GetMetadataReader();
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(fallbackReader);
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.True(set.TryResolveType(sourceReader, handle, out var first, out _));
        Assert.Same(fallbackReader, first);
        Assert.True(set.TryResolveType(sourceReader, handle, out var repeated, out _));
        Assert.Same(fallbackReader, repeated);
        set.Add(exactReader);
        Assert.True(set.TryResolveType(sourceReader, handle, out var latest, out _));
        Assert.Same(exactReader, latest);
    }

    [Fact]
    public void CompatibleFallback_RetriesResolverAndFindsLaterExactReader()
    {
        using var source = References();
        using var fallback = Definitions("Dependency", 2);
        using var exact = Definitions("Dependency");
        var resolver = new MutableResolver();
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible, resolver);
        set.Add(fallback.GetMetadataReader());
        var reader = source.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
        resolver.Target = exact.GetMetadataReader();
        Assert.True(set.TryResolveType(reader, handle, out var actual, out _));
        Assert.Same(resolver.Target, actual);
        Assert.Equal(3, resolver.Calls);
        Assert.Single(set.BindingSubstitutions);
    }

    [Fact]
    public void CompatibleNames_AreScopedToRequestingReader()
    {
        using var first = References(typeName: "First");
        using var second = References(typeName: "Second");
        using var target = Definitions("Dependency", typeName: "First");
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(target.GetMetadataReader());
        var firstReader = first.GetMetadataReader();
        var secondReader = second.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.True(set.TryResolveType(firstReader, handle, out _, out _));
        Assert.False(set.TryResolveType(secondReader, handle, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.TypeNotFound, reason);
        Assert.False(set.TryResolveType(secondReader, handle, out _, out _));
        Assert.True(set.TryResolveType(firstReader, handle, out _, out _));
    }

    [Theory]
    [InlineData(AssemblyBindingPolicy.Exact)]
    [InlineData(AssemblyBindingPolicy.ExactThenCompatible)]
    public void AssemblyIdentities_AreScopedToRequestingReader(AssemblyBindingPolicy policy)
    {
        using var first = References();
        using var second = References("Different");
        using var firstTarget = Definitions("Dependency");
        using var secondTarget = Definitions("Different");
        var resolver = new MutableResolver();
        using var set = new AssemblySet(policy, resolver);
        var firstReader = first.GetMetadataReader();
        var secondReader = second.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.False(set.TryResolveType(firstReader, handle, out _, out _));
        Assert.False(set.TryResolveType(secondReader, handle, out _, out _));
        var firstTargetReader = firstTarget.GetMetadataReader();
        var secondTargetReader = secondTarget.GetMetadataReader();
        resolver.Target = firstTargetReader;
        Assert.True(set.TryResolveType(firstReader, handle, out var actual, out _));
        Assert.Same(firstTargetReader, actual);
        resolver.Target = secondTargetReader;
        Assert.True(set.TryResolveType(secondReader, handle, out actual, out _));
        Assert.Same(secondTargetReader, actual);
    }

    [Fact]
    public void CompatibleDiagnostics_PreserveRequestAndRefreshedReaderCasing()
    {
        using var upper = References();
        using var lower = References("dependency");
        using var target = Definitions("Dependency", 2);
        using var refreshed = Definitions("DEPENDENCY", 2);
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(target.GetMetadataReader());
        var upperReader = upper.GetMetadataReader();
        var lowerReader = lower.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.True(set.TryResolveType(upperReader, handle, out _, out _));
        Assert.True(set.TryResolveType(lowerReader, handle, out _, out _));
        Assert.Equal(2, set.BindingSubstitutions.Count);
        Assert.StartsWith("Dependency,", set.BindingSubstitutions[0].RequestedIdentity);
        Assert.StartsWith("dependency,", set.BindingSubstitutions[1].RequestedIdentity);
        set.Add(refreshed.GetMetadataReader());
        Assert.True(set.TryResolveType(upperReader, handle, out _, out _));
        Assert.Equal(3, set.BindingSubstitutions.Count);
        Assert.StartsWith("DEPENDENCY,", set.BindingSubstitutions[2].ResolvedIdentity);
        set.ClearCaches();
        Assert.True(set.TryResolveType(upperReader, handle, out _, out _));
        Assert.Equal(3, set.BindingSubstitutions.Count);
    }

    [Fact]
    public void CompatibleDiagnostics_ObserveChangedPathOnRepeatedFallback()
    {
        using var source = References();
        using var target = Definitions("Dependency", 2);
        var resolver = new MutableResolver { Path = "first" };
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible, resolver);
        set.Add(target.GetMetadataReader());
        var reader = source.GetMetadataReader();
        var handle = MetadataTokens.TypeReferenceHandle(1);
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
        Assert.Single(set.BindingSubstitutions);
        resolver.Path = "second";
        Assert.True(set.TryResolveType(reader, handle, out _, out _));
        Assert.Equal(new[] { "first", "second" }, set.BindingSubstitutions.Select(item => item.Path));
        Assert.Equal(3, resolver.Calls);
    }

    [Fact]
    public void NestedReference_IgnoresInvalidUnusedNamespace()
    {
        var source = Create("Consumer");
        var assembly = source.AddAssemblyReference(source.GetOrAddString("Dependency"), new Version(1, 0, 0, 0), default, default, 0, default);
        var outer = source.AddTypeReference(assembly, source.GetOrAddString("Fixture"), source.GetOrAddString("Outer"));
        var nested = source.AddTypeReference(outer, source.GetOrAddString("Unused"), source.GetOrAddString("Inner"));
        var image = new BlobBuilder();
        new MetadataRootBuilder(source).Serialize(image, 0, 0);
        byte[] bytes = image.ToArray();
        using (var valid = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes)))
        {
            var validReader = valid.GetMetadataReader();
            Assert.Equal(6, validReader.GetTableRowSize(TableIndex.TypeRef));
            int namespaceOffset = validReader.GetTableMetadataOffset(TableIndex.TypeRef) + 6 + 4;
            bytes[namespaceOffset] = 0xff;
            bytes[namespaceOffset + 1] = 0xff;
        }
        using var provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes));
        var target = Create("Dependency");
        var outerDefinition = target.AddTypeDefinition(TypeAttributes.Public, target.GetOrAddString("Fixture"), target.GetOrAddString("Outer"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var innerDefinition = target.AddTypeDefinition(TypeAttributes.NestedPublic, default, target.GetOrAddString("Inner"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        target.AddNestedType(innerDefinition, outerDefinition);
        using var targetProvider = Serialize(target);
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(targetProvider.GetMetadataReader());
        var reader = provider.GetMetadataReader();
        Assert.True(set.TryResolveType(reader, nested, out _, out var actual));
        Assert.Equal(innerDefinition, actual);
        Assert.True(set.TryResolveType(reader, nested, out _, out actual));
        Assert.Equal(innerDefinition, actual);
    }

    private static MetadataReaderProvider Definitions(string name, int version = 1, string typeName = "Target")
    {
        var metadata = Create(name, version);
        metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString(typeName), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        return Serialize(metadata);
    }

    private static MetadataBuilder Create(string name, int version = 1)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(name), default, default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(version, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        return metadata;
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
    }

    private sealed class MutableResolver : IAssemblyReferenceResolver, IAssemblyPathProvider
    {
        public MetadataReader? Target;
        public int Calls;
        public Action? OnAttempt;
        public string? Path;

        public string? GetAssemblyPath(MetadataReader reader) => Path;

        public bool TryResolve(AssemblyReferenceHandle handle, MetadataReader requestingReader, out MetadataReader reader)
        {
            Calls++;
            OnAttempt?.Invoke();
            reader = Target!;
            return reader != null;
        }
    }
}

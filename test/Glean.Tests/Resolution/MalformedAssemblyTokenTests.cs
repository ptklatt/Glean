using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class MalformedAssemblyTokenTests
{
    public static IEnumerable<object[]> InvalidTokens()
    {
        foreach (int length in new[] { 1, 7, 9 })
        {
            foreach (var policy in new[] { AssemblyBindingPolicy.Exact, AssemblyBindingPolicy.ExactThenCompatible })
            {
                yield return new object[] { length, policy };
            }
        }
    }

    public static IEnumerable<object[]> CandidateCases()
    {
        foreach (var values in InvalidTokens())
        {
            yield return new object[] { values[0], values[1], false };
            yield return new object[] { values[0], values[1], true };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task Resolve_MalformedReference_ThrowsBeforeBinding(int length, AssemblyBindingPolicy policy)
    {
        using var fixture = new TokenFixture(new byte[length], []);
        var options = new AssemblyLoadOptions { BindingPolicy = policy, SearchPaths = [fixture.ValidDirectory] };
        using var loader = new AssemblyLoader(options);
        var entry = loader.LoadFile(fixture.EntryPath);
        var reference = Assert.Single(entry.AssemblyReferences);
        using var set = new AssemblySet(policy);
        set.Add(loader.LoadFile(fixture.ValidPath));

        for (int pass = 0; pass < 2; pass++)
        {
            Assert.Throws<BadImageFormatException>(() => set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out _, out _));
        }
        Assert.Empty(set.BindingSubstitutions);
        Assert.Throws<BadImageFormatException>(() => loader.TryResolve(reference, entry, out _));
        using var resolver = new DirectoryAssemblyResolver(options);
        Assert.Throws<BadImageFormatException>(() => resolver.TryResolve(reference, entry, out _));
        Assert.Throws<BadImageFormatException>(() => loader.LoadWithClosure(fixture.EntryPath));
        await Assert.ThrowsAsync<BadImageFormatException>(() => loader.LoadWithClosureAsync(fixture.EntryPath));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(9)]
    public void Add_MalformedDefinition_DoesNotRegisterReader(int length)
    {
        using var fixture = new TokenFixture([], new byte[length]);
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        using var set = new AssemblySet();

        Assert.Throws<BadImageFormatException>(() => set.Add(loader.LoadFile(fixture.WrongPath)));
        Assert.Empty(set.RegisteredAssemblies);
        var valid = loader.LoadFile(fixture.ValidPath);
        set.Add(valid);
        Assert.True(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out var resolved, out _));
        Assert.Same(valid, resolved);
    }

    [Theory]
    [MemberData(nameof(CandidateCases))]
    public async Task LoadWithClosure_MalformedCandidate_ContinuesToValidIdentity(
        int length, AssemblyBindingPolicy policy, bool asynchronous)
    {
        using var fixture = new TokenFixture([], new byte[length]);
        int majorVersion = policy == AssemblyBindingPolicy.ExactThenCompatible ? 2 : 1;
        WriteImage(fixture.ValidPath, [], majorVersion: majorVersion);
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            BindingPolicy = policy,
            SearchPaths = [fixture.WrongDirectory, fixture.ValidDirectory]
        });
        _ = loader.LoadFile(fixture.WrongPath);
        var failures = new List<AssemblyDependencyLoadFailure>();

        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Empty(failures);
        Assert.Equal(2, set.RegisteredAssemblies.Count);
        Assert.True(set.TryResolveType(result.EntryReader, Assert.Single(result.EntryReader.TypeReferences), out var resolved, out _));
        Assert.Same(loader.LoadFile(fixture.ValidPath), resolved);
        Assert.Equal(majorVersion, resolved.GetAssemblyDefinition().Version.Major);
        Assert.Equal(policy == AssemblyBindingPolicy.ExactThenCompatible ? 1 : 0, set.BindingSubstitutions.Count);
    }

    [Theory]
    [InlineData(AssemblyBindingPolicy.Exact, false)]
    [InlineData(AssemblyBindingPolicy.Exact, true)]
    [InlineData(AssemblyBindingPolicy.ExactThenCompatible, false)]
    [InlineData(AssemblyBindingPolicy.ExactThenCompatible, true)]
    public async Task LoadWithClosure_OnlyMalformedCandidate_ReportsBadImageFormat(
        AssemblyBindingPolicy policy, bool asynchronous)
    {
        using var fixture = new TokenFixture([], new byte[7]);
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            BindingPolicy = policy,
            SearchPaths = [fixture.WrongDirectory]
        });
        var failures = new List<AssemblyDependencyLoadFailure>();

        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        var failure = Assert.Single(failures);
        Assert.Equal(AssemblyDependencyLoadFailureKind.BadImageFormat, failure.Kind);
        Assert.Equal(fixture.WrongPath, failure.Path);
        Assert.Same(result.EntryReader, Assert.Single(set.RegisteredAssemblies));
        Assert.Empty(set.BindingSubstitutions);
    }

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public void DirectoryResolver_MalformedCachedCandidate_DoesNotBlockAddedPath(int length, AssemblyBindingPolicy policy)
    {
        using var fixture = new TokenFixture([], new byte[length]);
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        var reference = Assert.Single(entry.AssemblyReferences);
        using var resolver = new DirectoryAssemblyResolver(new AssemblyLoadOptions
        {
            BindingPolicy = policy,
            SearchPaths = [fixture.WrongDirectory]
        });

        Assert.False(resolver.TryResolve(reference, entry, out var rejected));
        Assert.Null(rejected);
        resolver.AddSearchPath(fixture.ValidDirectory);
        Assert.True(resolver.TryResolve(reference, entry, out var resolved));
        Assert.NotNull(resolved);
        Assert.Equal(0, resolved.GetBlobReader(resolved.GetAssemblyDefinition().PublicKey).Length);
        Assert.True(resolver.TryResolve(reference, entry, out var cached));
        Assert.Same(resolved, cached);
    }

    [Theory]
    [InlineData("unsigned")]
    [InlineData("token")]
    [InlineData("full-key")]
    public void Resolve_ValidUnsignedTokenAndFullKeyIdentities_KeepMatching(string kind)
    {
        var runtime = typeof(object).Assembly.GetName();
        byte[] definitionKey = kind == "unsigned" ? [] : runtime.GetPublicKey()!;
        byte[] referenceKey = kind == "unsigned" ? [] : kind == "token" ? runtime.GetPublicKeyToken()! : definitionKey;
        var definitionFlags = kind == "unsigned" ? 0 : AssemblyFlags.PublicKey;
        var referenceFlags = kind == "full-key" ? AssemblyFlags.PublicKey : 0;
        using var fixture = new TokenFixture(referenceKey, [], referenceFlags);
        WriteImage(fixture.ValidPath, definitionKey, definitionFlags);
        using var loader = new AssemblyLoader(fixture.ValidDirectory);
        var entry = loader.LoadFile(fixture.EntryPath);
        var reference = Assert.Single(entry.AssemblyReferences);
        var definition = loader.LoadFile(fixture.ValidPath);
        using var set = new AssemblySet();
        set.Add(definition);

        Assert.True(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out var resolved, out _));
        Assert.Same(definition, resolved);
        Assert.True(loader.TryResolve(reference, entry, out resolved));
        Assert.Same(definition, resolved);
        using var resolver = new DirectoryAssemblyResolver(fixture.ValidDirectory);
        Assert.True(resolver.TryResolve(reference, entry, out resolved));
        Assert.NotNull(resolved);
    }

    private sealed class TokenFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace = new();

        public TokenFixture(byte[] referenceKey, byte[] candidateKey, AssemblyFlags referenceFlags = 0)
        {
            EntryPath = Path.Combine(_workspace.CreateDirectory("entry"), "Entry.dll");
            WrongDirectory = _workspace.CreateDirectory("wrong");
            ValidDirectory = _workspace.CreateDirectory("valid");
            WrongPath = Path.Combine(WrongDirectory, "Dependency.dll");
            ValidPath = Path.Combine(ValidDirectory, "Dependency.dll");
            WriteImage(EntryPath, [], referenceKey: referenceKey, referenceFlags: referenceFlags);
            WriteImage(WrongPath, candidateKey);
            WriteImage(ValidPath, []);
        }

        public string EntryPath { get; }
        public string WrongDirectory { get; }
        public string ValidDirectory { get; }
        public string WrongPath { get; }
        public string ValidPath { get; }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }

    private static void WriteImage(string path, byte[] key, AssemblyFlags flags = 0,
        byte[]? referenceKey = null, AssemblyFlags referenceFlags = 0, int majorVersion = 1)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(Path.GetFileName(path)), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(Path.GetFileNameWithoutExtension(path)), new Version(majorVersion, 0, 0, 0),
            default, metadata.GetOrAddBlob(key), flags, AssemblyHashAlgorithm.None);
        if (referenceKey != null)
        {
            var reference = metadata.AddAssemblyReference(metadata.GetOrAddString("Dependency"), new Version(1, 0, 0, 0),
                default, metadata.GetOrAddBlob(referenceKey), referenceFlags, default);
            metadata.AddTypeReference(reference, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Item"));
        }
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Item"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var image = new BlobBuilder();
        new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(),
            flags: CorFlags.ILOnly).Serialize(image);
        File.WriteAllBytes(path, image.ToArray());
    }
}

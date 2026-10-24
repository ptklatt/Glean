using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyIdentityResolutionTests
{
    private static readonly string[] Mismatches = ["name", "version", "culture", "token", "unsigned", "module"];

    public static IEnumerable<object[]> MismatchCases()
    {
        foreach (var mismatch in Mismatches)
        {
            yield return [mismatch, false];
            yield return [mismatch, true];
        }
    }

    public static IEnumerable<object[]> ProbeCases()
    {
        foreach (var values in MismatchCases())
        {
            yield return [values[0], values[1], false];
        }

        yield return ["version", false, true];
        yield return ["version", true, true];
    }

    [Theory]
    [MemberData(nameof(ProbeCases))]
    public async Task LoadWithClosure_MismatchedCandidate_UsesLaterExactCandidate(
        string mismatch,
        bool asynchronous,
        bool preloadCandidate)
    {
        using var fixture = new IdentityFixture(mismatch);
        using var loader = new AssemblyLoader(fixture.WrongDirectory, fixture.ExactDirectory);
        if (preloadCandidate)
        {
            _ = loader.LoadFile(fixture.WrongPath);
        }

        var failures = new List<AssemblyDependencyLoadFailure>();
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Empty(failures);
        Assert.Equal(2, set.RegisteredAssemblies.Count);
        var dependency = Assert.Single(set.RegisteredAssemblies, reader => !ReferenceEquals(reader, result.EntryReader));
        Assert.Equal(fixture.ExactModuleId, dependency.GetGuid(dependency.GetModuleDefinition().Mvid));
    }

    [Theory]
    [MemberData(nameof(MismatchCases))]
    public async Task LoadWithClosure_OnlyMismatchedCandidate_ReportsFailureAndStaysPartial(
        string mismatch,
        bool asynchronous)
    {
        using var fixture = new IdentityFixture(mismatch);
        var expectedKind = mismatch == "module"
            ? AssemblyDependencyLoadFailureKind.NotAssembly
            : AssemblyDependencyLoadFailureKind.IdentityMismatch;

        if (asynchronous)
        {
            using var loader = new AssemblyLoader(fixture.WrongDirectory);
            var failures = new List<AssemblyDependencyLoadFailure>();
            var result = await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add);
            using var set = result.Set;

            var failure = Assert.Single(failures);
            Assert.Equal(expectedKind, failure.Kind);
            Assert.Equal("Dependency", failure.AssemblySimpleName);
            Assert.Equal(fixture.WrongPath, failure.Path);
            Assert.Same(result.EntryReader, Assert.Single(set.RegisteredAssemblies));
        }
        else
        {
            using var closure = AssemblyClosure.Load(fixture.EntryPath, fixture.WrongDirectory);

            var failure = Assert.Single(closure.SkippedDependencies);
            Assert.Equal(expectedKind, failure.Kind);
            Assert.Equal("Dependency", failure.AssemblySimpleName);
            Assert.Equal(fixture.WrongPath, failure.Path);
            Assert.Same(closure.EntryReader, Assert.Single(closure.Set.RegisteredAssemblies));
            var exception = Assert.Throws<PartialClosureException>(() => closure.ThrowIfPartial());
            Assert.Equal(expectedKind, Assert.Single(exception.SkippedDependencies).Kind);
        }
    }

    [Theory]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("culture")]
    [InlineData("token")]
    [InlineData("unsigned")]
    [InlineData("module")]
    public void TryResolve_MismatchedCandidate_ContinuesProbingAfterReaderIsCached(string mismatch)
    {
        using var fixture = new IdentityFixture(mismatch);
        using var loader = new AssemblyLoader();
        var entryReader = loader.LoadFile(fixture.EntryPath);
        var reference = Assert.Single(entryReader.AssemblyReferences);
        using var resolver = new DirectoryAssemblyResolver(fixture.WrongDirectory);

        Assert.False(resolver.TryResolve(reference, entryReader, out var rejected));
        Assert.Null(rejected);

        resolver.AddSearchPath(fixture.ExactDirectory);

        Assert.True(resolver.TryResolve(reference, entryReader, out var resolved));
        Assert.NotNull(resolved);
        Assert.Equal(fixture.ExactModuleId, resolved.GetGuid(resolved.GetModuleDefinition().Mvid));
        Assert.True(resolver.TryResolve(reference, entryReader, out var cached));
        Assert.Same(resolved, cached);
    }

    [Theory]
    [InlineData("", "neutral", false)]
    [InlineData("neutral", "", true)]
    [InlineData("fr-FR", "FR-fr", false)]
    [InlineData("fr-FR", "fr-FR", true)]
    public async Task ExactIdentity_NormalizedNameCultureAndKey_AreAcceptedByEveryLoadingPath(
        string referenceCulture,
        string definitionCulture,
        bool referenceUsesFullKey)
    {
        using var fixture = new IdentityFixture("version", referenceCulture, definitionCulture, referenceUsesFullKey);
        using var syncLoader = new AssemblyLoader(fixture.ExactDirectory);
        using var asyncLoader = new AssemblyLoader(fixture.ExactDirectory);
        var failures = new List<AssemblyDependencyLoadFailure>();
        var syncResult = syncLoader.LoadWithClosure(fixture.EntryPath, failures.Add);
        var asyncResult = await asyncLoader.LoadWithClosureAsync(fixture.EntryPath, failures.Add);
        using var syncSet = syncResult.Set;
        using var asyncSet = asyncResult.Set;

        Assert.Empty(failures);
        foreach (var reader in syncSet.RegisteredAssemblies.Concat(asyncSet.RegisteredAssemblies))
        {
            if (ReferenceEquals(reader, syncResult.EntryReader) || ReferenceEquals(reader, asyncResult.EntryReader))
            {
                continue;
            }

            Assert.Equal(fixture.ExactModuleId, reader.GetGuid(reader.GetModuleDefinition().Mvid));
        }
        Assert.Equal(2, syncSet.RegisteredAssemblies.Count);
        Assert.Equal(2, asyncSet.RegisteredAssemblies.Count);

        using var resolver = new DirectoryAssemblyResolver(fixture.ExactDirectory);
        Assert.True(resolver.TryResolve(Assert.Single(syncResult.EntryReader.AssemblyReferences), syncResult.EntryReader, out var resolved));
        Assert.NotNull(resolved);
        Assert.Equal(fixture.ExactModuleId, resolved.GetGuid(resolved.GetModuleDefinition().Mvid));
    }

    private readonly record struct Identity(string Name, Version Version, string Culture, byte[] Key, AssemblyFlags Flags);

    private sealed class IdentityFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace = new();

        public IdentityFixture(
            string mismatch,
            string referenceCulture = "fr-FR",
            string definitionCulture = "fr-FR",
            bool referenceUsesFullKey = false)
        {
            WrongDirectory = _workspace.CreateDirectory("wrong");
            ExactDirectory = _workspace.CreateDirectory("exact");
            EntryPath = Path.Combine(_workspace.CreateDirectory("entry"), "Entry.dll");
            WrongPath = Path.Combine(WrongDirectory, "Dependency.dll");
            ExactModuleId = Guid.NewGuid();

            var runtimeIdentity = typeof(object).Assembly.GetName();
            var key = runtimeIdentity.GetPublicKey()!;
            var definition = new Identity("dependency", new Version(1, 2, 3, 4), definitionCulture, key, AssemblyFlags.PublicKey);
            var reference = definition with
            {
                Name = "Dependency",
                Culture = referenceCulture,
                Key = referenceUsesFullKey ? key : runtimeIdentity.GetPublicKeyToken()!,
                Flags = referenceUsesFullKey ? AssemblyFlags.PublicKey : 0
            };
            var otherKey = (byte[])key.Clone();
            otherKey[^1] ^= 0xff;
            var wrongDefinition = mismatch switch
            {
                "name" => definition with { Name = "OtherDependency" },
                "version" => definition with { Version = new Version(2, 0, 0, 0) },
                "culture" => definition with { Culture = "de-DE" },
                "token" => definition with { Key = otherKey },
                "unsigned" => definition with { Key = [], Flags = 0 },
                "module" => definition,
                _ => throw new ArgumentException("Unknown identity mismatch.", nameof(mismatch))
            };

            var entry = new Identity("Entry", new Version(1, 0, 0, 0), string.Empty, [], 0);
            WriteImage(EntryPath, entry, Guid.NewGuid(), reference);
            WriteImage(Path.Combine(ExactDirectory, "Dependency.dll"), definition, ExactModuleId);
            WriteImage(WrongPath, wrongDefinition, Guid.NewGuid(), isModule: mismatch == "module");
        }

        public string WrongDirectory { get; }
        public string ExactDirectory { get; }
        public string EntryPath { get; }
        public string WrongPath { get; }
        public Guid ExactModuleId { get; }

        public void Dispose()
        {
            _workspace.Dispose();
        }

        private static void WriteImage(string path, Identity identity, Guid moduleId, Identity? reference = null, bool isModule = false)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(0, metadata.GetOrAddString(Path.GetFileName(path)), metadata.GetOrAddGuid(moduleId), default, default);
            if (!isModule)
            {
                metadata.AddAssembly(
                    metadata.GetOrAddString(identity.Name), identity.Version,
                    metadata.GetOrAddString(identity.Culture), metadata.GetOrAddBlob(identity.Key),
                    identity.Flags, AssemblyHashAlgorithm.None);
            }

            if (reference.HasValue)
            {
                var dependency = reference.Value;
                metadata.AddAssemblyReference(
                    metadata.GetOrAddString(dependency.Name), dependency.Version,
                    metadata.GetOrAddString(dependency.Culture), metadata.GetOrAddBlob(dependency.Key),
                    dependency.Flags, default);
            }

            metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(), flags: CorFlags.ILOnly);
            var image = new BlobBuilder();
            pe.Serialize(image);
            File.WriteAllBytes(path, image.ToArray());
        }
    }
}

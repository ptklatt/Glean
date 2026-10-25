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
    private static readonly string[] Mismatches = ["name", "version", "culture", "token", "unsigned", "signed", "module"];

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
    [InlineData("signed")]
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_ExhaustsExactCandidatesBeforeFallback(bool asynchronous)
    {
        using var fixture = new IdentityFixture("version");
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory, fixture.ExactDirectory]
        });
        _ = loader.LoadFile(fixture.WrongPath);
        var failures = new List<AssemblyDependencyLoadFailure>();
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Empty(failures);
        Assert.Empty(set.BindingSubstitutions);
        Assert.True(set.TryResolveType(result.EntryReader, Assert.Single(result.EntryReader.TypeReferences), out var resolved, out _));
        Assert.Equal(fixture.ExactModuleId, resolved.GetGuid(resolved.GetModuleDefinition().Mvid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_VersionFallbackIsObservableAndNotSkipped(bool asynchronous)
    {
        using var fixture = new IdentityFixture("version");
        var options = new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory]
        };
        var failures = new List<AssemblyDependencyLoadFailure>();
        using var loader = new AssemblyLoader(options);
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Empty(failures);
        Assert.Equal(2, set.RegisteredAssemblies.Count);
        AssertSubstitution(Assert.Single(set.BindingSubstitutions), fixture.WrongPath);
        Assert.True(set.TryResolveType(result.EntryReader, Assert.Single(result.EntryReader.TypeReferences), out var resolved, out _));
        Assert.Equal(fixture.WrongModuleId, resolved.GetGuid(resolved.GetModuleDefinition().Mvid));
        Assert.Single(set.BindingSubstitutions);

        using var closure = AssemblyClosure.Load(fixture.EntryPath, options, failures.Add);
        Assert.Same(closure, closure.ThrowIfPartial());
        Assert.Empty(failures);
        AssertSubstitution(Assert.Single(closure.BindingSubstitutions), fixture.WrongPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_LowerVersionFallbackRecordsBothIdentities(bool asynchronous)
    {
        using var fixture = new IdentityFixture("version");
        var lowerVersion = new Version(1, 0, 0, 0);
        var lowerPath = fixture.WriteVersion("lower", lowerVersion);
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [Path.GetDirectoryName(lowerPath)!]
        });
        var failures = new List<AssemblyDependencyLoadFailure>();
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add)
            : loader.LoadWithClosure(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Empty(failures);
        Assert.Equal(2, set.RegisteredAssemblies.Count);
        var substitution = Assert.Single(set.BindingSubstitutions);
        var token = Convert.ToHexString(typeof(object).Assembly.GetName().GetPublicKeyToken()!).ToLowerInvariant();
        Assert.Equal($"Dependency, Version=1.2.3.4, Culture=fr-FR, PublicKeyToken={token}", substitution.RequestedIdentity);
        Assert.Equal($"dependency, Version=1.0.0.0, Culture=fr-FR, PublicKeyToken={token}", substitution.ResolvedIdentity);
        Assert.Equal(lowerPath, substitution.Path);
        Assert.True(set.TryResolveType(result.EntryReader, Assert.Single(result.EntryReader.TypeReferences), out var resolved, out _));
        Assert.Equal(lowerVersion, resolved.GetAssemblyDefinition().Version);
        Assert.Single(set.BindingSubstitutions);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("culture")]
    [InlineData("token")]
    [InlineData("unsigned")]
    [InlineData("signed")]
    [InlineData("module")]
    public async Task CompatibleBinding_RejectsEveryNonVersionMismatch(string mismatch)
    {
        using var fixture = new IdentityFixture(mismatch);
        var options = new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory]
        };
        using var loader = new AssemblyLoader(options);
        var failures = new List<AssemblyDependencyLoadFailure>();
        var result = await loader.LoadWithClosureAsync(fixture.EntryPath, failures.Add);
        using var set = result.Set;

        Assert.Single(failures);
        Assert.Empty(set.BindingSubstitutions);
        Assert.Single(set.RegisteredAssemblies);
        Assert.False(set.TryResolveType(result.EntryReader, Assert.Single(result.EntryReader.TypeReferences), out _, out _));
        using var closure = AssemblyClosure.Load(fixture.EntryPath, options);
        Assert.Single(closure.SkippedDependencies);
        Assert.Empty(closure.BindingSubstitutions);
    }

    [Theory]
    [InlineData("", "neutral", false)]
    [InlineData("neutral", "", true)]
    [InlineData("fr-FR", "FR-fr", false)]
    [InlineData("fr-FR", "fr-FR", true)]
    public void CompatibleBinding_NormalizesCultureAndFullKeys(string referenceCulture, string definitionCulture, bool fullKey)
    {
        using var fixture = new IdentityFixture("version", referenceCulture, definitionCulture, fullKey);
        using var closure = AssemblyClosure.Load(fixture.EntryPath, new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory]
        });

        Assert.Empty(closure.SkippedDependencies);
        Assert.Single(closure.BindingSubstitutions);
        Assert.True(closure.Set.TryResolveType(closure.EntryReader, Assert.Single(closure.EntryReader.TypeReferences), out _, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompatibleBinding_UsesProbeOrderRegardlessOfLoadedOrderOrHighestVersion(bool asynchronous)
    {
        using var fixture = new IdentityFixture("version");
        var higherPath = fixture.WriteVersion("higher", new Version(9, 0, 0, 0));
        using var loader = new AssemblyLoader(new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory, Path.GetDirectoryName(higherPath)!]
        });
        _ = loader.LoadFile(higherPath);
        var result = asynchronous
            ? await loader.LoadWithClosureAsync(fixture.EntryPath)
            : loader.LoadWithClosure(fixture.EntryPath);
        using var set = result.Set;

        AssertSubstitution(Assert.Single(set.BindingSubstitutions), fixture.WrongPath);
        Assert.True(set.TryResolveType(result.EntryReader, Assert.Single(result.EntryReader.TypeReferences), out var resolved, out _));
        Assert.Equal(fixture.WrongModuleId, resolved.GetGuid(resolved.GetModuleDefinition().Mvid));
    }

    [Fact]
    public void CompatibleBinding_WarmedResolverReprobesForLaterExactCandidate()
    {
        using var fixture = new IdentityFixture("version");
        using var entryLoader = new AssemblyLoader();
        var entry = entryLoader.LoadFile(fixture.EntryPath);
        using var resolver = new DirectoryAssemblyResolver(new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory]
        });
        var reference = Assert.Single(entry.AssemblyReferences);
        Assert.True(resolver.TryResolve(reference, entry, out var fallback));
        Assert.Equal(fixture.WrongModuleId, fallback!.GetGuid(fallback.GetModuleDefinition().Mvid));

        resolver.AddSearchPath(fixture.ExactDirectory);

        Assert.True(resolver.TryResolve(reference, entry, out var exact));
        Assert.Equal(fixture.ExactModuleId, exact!.GetGuid(exact.GetModuleDefinition().Mvid));
    }

    [Fact]
    public void CompatibleBinding_WarmedTypeAndMemberRebindWhenExactBecomesAvailable()
    {
        using var fixture = new IdentityFixture("version");
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        using var resolver = new DirectoryAssemblyResolver(new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            SearchPaths = [fixture.WrongDirectory]
        });
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible, resolver);
        var type = Assert.Single(entry.TypeReferences);
        var member = Assert.Single(entry.MemberReferences);
        Assert.True(set.TryResolveType(entry, type, out var fallbackType, out _));
        Assert.True(set.TryResolveMember(entry, member, out var fallbackMember, out _));
        Assert.Equal(fixture.WrongModuleId, fallbackType.GetGuid(fallbackType.GetModuleDefinition().Mvid));
        Assert.Same(fallbackType, fallbackMember);
        AssertSubstitution(Assert.Single(set.BindingSubstitutions), fixture.WrongPath);

        resolver.AddSearchPath(fixture.ExactDirectory);

        Assert.True(set.TryResolveType(entry, type, out var exactType, out _));
        Assert.True(set.TryResolveMember(entry, member, out var exactMember, out _));
        Assert.Equal(fixture.ExactModuleId, exactType.GetGuid(exactType.GetModuleDefinition().Mvid));
        Assert.Same(exactType, exactMember);
        Assert.Single(set.BindingSubstitutions);
    }

    [Theory]
    [InlineData("name", AssemblyBindingPolicy.Exact)]
    [InlineData("version", AssemblyBindingPolicy.Exact)]
    [InlineData("culture", AssemblyBindingPolicy.Exact)]
    [InlineData("token", AssemblyBindingPolicy.Exact)]
    [InlineData("unsigned", AssemblyBindingPolicy.Exact)]
    [InlineData("signed", AssemblyBindingPolicy.Exact)]
    [InlineData("module", AssemblyBindingPolicy.Exact)]
    [InlineData("name", AssemblyBindingPolicy.ExactThenCompatible)]
    [InlineData("culture", AssemblyBindingPolicy.ExactThenCompatible)]
    [InlineData("token", AssemblyBindingPolicy.ExactThenCompatible)]
    [InlineData("unsigned", AssemblyBindingPolicy.ExactThenCompatible)]
    [InlineData("signed", AssemblyBindingPolicy.ExactThenCompatible)]
    [InlineData("module", AssemblyBindingPolicy.ExactThenCompatible)]
    public void AssemblySet_ValidatesCustomResolverResults(string mismatch, AssemblyBindingPolicy policy)
    {
        using var fixture = new IdentityFixture(mismatch);
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        var wrong = loader.LoadFile(fixture.WrongPath);
        using var set = new AssemblySet(policy, new FixedResolver(wrong));

        Assert.False(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
        Assert.Empty(set.BindingSubstitutions);
        Assert.Empty(set.RegisteredAssemblies);
    }

    [Fact]
    public void AssemblySet_RegisteredFallbackCannotMaskExactCustomResolver()
    {
        using var fixture = new IdentityFixture("version");
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        var wrong = loader.LoadFile(fixture.WrongPath);
        var exact = loader.LoadFile(Path.Combine(fixture.ExactDirectory, "Dependency.dll"));
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible, new FixedResolver(exact));
        set.Add(wrong);

        Assert.True(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out var resolved, out _));
        Assert.Same(exact, resolved);
        Assert.Empty(set.BindingSubstitutions);
    }

    [Fact]
    public void AssemblySet_CustomVersionFallbackRequiresPolicyAndRecordsIdentity()
    {
        using var fixture = new IdentityFixture("version");
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        var wrong = loader.LoadFile(fixture.WrongPath);
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible, new FixedResolver(wrong));

        Assert.True(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out var resolved, out _));
        Assert.Same(wrong, resolved);
        var substitution = Assert.Single(set.BindingSubstitutions);
        Assert.Contains("Version=1.2.3.4", substitution.RequestedIdentity);
        Assert.Contains("Version=2.0.0.0", substitution.ResolvedIdentity);
        Assert.Null(substitution.Path);
    }

    [Fact]
    public void AssemblySet_RejectedCustomResultDoesNotPreventValidRegisteredFallback()
    {
        using var fixture = new IdentityFixture("culture");
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        var wrongCulture = loader.LoadFile(fixture.WrongPath);
        var compatible = loader.LoadFile(fixture.WriteVersion("compatible", new Version(2, 0, 0, 0)));
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible, new FixedResolver(wrongCulture));
        set.Add(compatible);

        Assert.True(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out var resolved, out _));
        Assert.Same(compatible, resolved);
        Assert.Single(set.RegisteredAssemblies);
        Assert.Single(set.BindingSubstitutions);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("culture")]
    [InlineData("token")]
    [InlineData("unsigned")]
    [InlineData("signed")]
    public void AssemblySet_RegisteredFallbackRejectsNonVersionMismatches(string mismatch)
    {
        using var fixture = new IdentityFixture(mismatch);
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(loader.LoadFile(fixture.WrongPath));

        Assert.False(set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out _, out _));
        Assert.Empty(set.BindingSubstitutions);
    }

    [Theory]
    [InlineData(AssemblyBindingPolicy.Exact, false)]
    [InlineData(AssemblyBindingPolicy.ExactThenCompatible, true)]
    public void AssemblySet_RegisteredVersionsRequireExplicitPolicy(AssemblyBindingPolicy policy, bool expected)
    {
        using var fixture = new IdentityFixture("version");
        using var loader = new AssemblyLoader();
        var entry = loader.LoadFile(fixture.EntryPath);
        var wrong = loader.LoadFile(fixture.WrongPath);
        var higherPath = fixture.WriteVersion("higher", new Version(9, 0, 0, 0));
        using var set = new AssemblySet(policy);
        set.Add(wrong);
        set.Add(loader.LoadFile(higherPath));

        Assert.Equal(expected, set.TryResolveType(entry, Assert.Single(entry.TypeReferences), out var resolved, out _));
        if (expected)
        {
            Assert.Same(wrong, resolved);
            Assert.Null(Assert.Single(set.BindingSubstitutions).Path);
        }
        else
        {
            Assert.Empty(set.BindingSubstitutions);
        }
    }

    [Fact]
    public void ClosureOptions_ProbeTargetFrameworkAndSnapshotMutableSearchPaths()
    {
        using var fixture = new IdentityFixture("version");
        var layoutPath = fixture.WriteVersion(Path.Combine("package", "lib", "net10.0"), new Version(2, 0, 0, 0));
        var packagePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(layoutPath)))!;
        var paths = new[] { packagePath };
        var options = new AssemblyLoadOptions
        {
            BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible,
            TargetFrameworkMoniker = "net10.0",
            SearchPaths = paths
        };
        using var loader = new AssemblyLoader(options);
        paths[0] = fixture.ExactDirectory;
        var result = loader.LoadWithClosure(fixture.EntryPath);
        using var set = result.Set;
        Assert.Equal(layoutPath, Assert.Single(set.BindingSubstitutions).Path);

        paths[0] = packagePath;
        using var closure = AssemblyClosure.Load(fixture.EntryPath, options);
        Assert.Empty(closure.SkippedDependencies);
        Assert.Equal(layoutPath, Assert.Single(closure.BindingSubstitutions).Path);
    }

    [Fact]
    public void SharedLoader_ClosureLazyResolutionKeepsEntryDirectoriesAndDiagnosticsSeparate()
    {
        using var first = new IdentityFixture("version");
        using var second = new IdentityFixture("version");
        using var loader = new AssemblyLoader(new AssemblyLoadOptions { BindingPolicy = AssemblyBindingPolicy.ExactThenCompatible });
        var firstResult = loader.LoadWithClosure(first.EntryPath);
        var secondResult = loader.LoadWithClosure(second.EntryPath);
        using var firstSet = firstResult.Set;
        using var secondSet = secondResult.Set;
        Assert.Empty(firstSet.BindingSubstitutions);
        Assert.Empty(secondSet.BindingSubstitutions);
        var firstPath = first.WriteVersion("entry", new Version(2, 0, 0, 0));
        var secondPath = second.WriteVersion("entry", new Version(3, 0, 0, 0));

        Assert.True(firstSet.TryResolveType(firstResult.EntryReader, Assert.Single(firstResult.EntryReader.TypeReferences), out var firstResolved, out _));
        Assert.True(secondSet.TryResolveType(secondResult.EntryReader, Assert.Single(secondResult.EntryReader.TypeReferences), out var secondResolved, out _));
        Assert.NotSame(firstResolved, secondResolved);
        Assert.Equal(firstPath, Assert.Single(firstSet.BindingSubstitutions).Path);
        Assert.Equal(secondPath, Assert.Single(secondSet.BindingSubstitutions).Path);
        Assert.Contains("Version=2.0.0.0", firstSet.BindingSubstitutions[0].ResolvedIdentity);
        Assert.Contains("Version=3.0.0.0", secondSet.BindingSubstitutions[0].ResolvedIdentity);
    }

    [Fact]
    public void Options_ValidatePolicyAndSearchPaths()
    {
        Assert.Throws<ArgumentNullException>(() => new AssemblyLoader((AssemblyLoadOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new AssemblyLoader(new AssemblyLoadOptions { SearchPaths = null! }));
        Assert.Throws<ArgumentException>(() => new AssemblyLoader(new AssemblyLoadOptions { SearchPaths = [""] }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssemblyLoader(new AssemblyLoadOptions { BindingPolicy = (AssemblyBindingPolicy)99 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssemblySet((AssemblyBindingPolicy)99));
    }

    private static void AssertSubstitution(AssemblyBindingSubstitution substitution, string path)
    {
        var token = Convert.ToHexString(typeof(object).Assembly.GetName().GetPublicKeyToken()!).ToLowerInvariant();
        Assert.Equal($"Dependency, Version=1.2.3.4, Culture=fr-FR, PublicKeyToken={token}", substitution.RequestedIdentity);
        Assert.Equal($"dependency, Version=2.0.0.0, Culture=fr-FR, PublicKeyToken={token}", substitution.ResolvedIdentity);
        Assert.Equal(path, substitution.Path);
    }

    private sealed class FixedResolver(MetadataReader reader) : IAssemblyReferenceResolver
    {
        public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            resolvedReader = reader;
            return true;
        }
    }

    private readonly record struct Identity(string Name, Version Version, string Culture, byte[] Key, AssemblyFlags Flags);

    private sealed class IdentityFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace = new();
        private readonly Identity _definition;

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
            WrongModuleId = Guid.NewGuid();

            var runtimeIdentity = typeof(object).Assembly.GetName();
            var key = runtimeIdentity.GetPublicKey()!;
            var definition = new Identity("dependency", new Version(1, 2, 3, 4), definitionCulture, key, AssemblyFlags.PublicKey);
            if (mismatch == "signed")
            {
                definition = definition with { Key = [], Flags = 0 };
            }
            _definition = definition;
            var reference = definition with
            {
                Name = "Dependency",
                Culture = referenceCulture,
                Key = definition.Key.Length == 0 ? [] : referenceUsesFullKey ? key : runtimeIdentity.GetPublicKeyToken()!,
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
                "signed" => definition with { Key = key, Flags = AssemblyFlags.PublicKey },
                "module" => definition,
                _ => throw new ArgumentException("Unknown identity mismatch.", nameof(mismatch))
            };

            var entry = new Identity("Entry", new Version(1, 0, 0, 0), string.Empty, [], 0);
            WriteImage(EntryPath, entry, Guid.NewGuid(), reference);
            WriteImage(Path.Combine(ExactDirectory, "Dependency.dll"), definition, ExactModuleId);
            WriteImage(WrongPath, wrongDefinition, WrongModuleId, isModule: mismatch == "module");
        }

        public string WrongDirectory { get; }
        public string ExactDirectory { get; }
        public string EntryPath { get; }
        public string WrongPath { get; }
        public Guid ExactModuleId { get; }
        public Guid WrongModuleId { get; }

        public string WriteVersion(string directory, Version version)
        {
            var path = Path.Combine(_workspace.CreateDirectory(directory), "Dependency.dll");
            WriteImage(path, _definition with { Version = version }, Guid.NewGuid());
            return path;
        }

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
                var assemblyReference = metadata.AddAssemblyReference(
                    metadata.GetOrAddString(dependency.Name), dependency.Version,
                    metadata.GetOrAddString(dependency.Culture), metadata.GetOrAddBlob(dependency.Key),
                    dependency.Flags, default);
                var typeReference = metadata.AddTypeReference(assemblyReference, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("DependencyType"));
                metadata.AddMemberReference(typeReference, metadata.GetOrAddString("Method"), metadata.GetOrAddBlob(new byte[] { 0x20, 0, 0x01 }));
            }

            metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Abstract,
                metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("DependencyType"), default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.NewSlot,
                MethodImplAttributes.IL, metadata.GetOrAddString("Method"), metadata.GetOrAddBlob(new byte[] { 0x20, 0, 0x01 }),
                -1, MetadataTokens.ParameterHandle(1));
            var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(), flags: CorFlags.ILOnly);
            var image = new BlobBuilder();
            pe.Serialize(image);
            File.WriteAllBytes(path, image.ToArray());
        }
    }
}

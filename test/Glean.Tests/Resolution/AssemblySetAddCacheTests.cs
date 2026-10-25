using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblySetAddCacheTests
{
    private const TypeAttributes ForwarderTypeAttribute = (TypeAttributes)0x00200000;

    [Fact]
    public void Add_AfterFailedTypeAndMemberLookup_ResolvesWithoutManualClear()
    {
        using var workspace = new TemporaryDirectory();
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Dependency",
            "namespace Fixture; public sealed class Dependency { public int Echo(int value) => value; }");
        using var dependency = new MetadataScope(dependencyPath);
        using var consumer = new TestAssemblyBuilder("Consumer")
            .WithReference(dependencyPath)
            .WithSource("namespace Fixture; public sealed class Consumer { public int Run(Dependency value) => value.Echo(42); }")
            .BuildMetadataScope();
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "Dependency");
        var memberReference = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "Dependency", "Echo");
        using var set = new AssemblySet();

        Assert.False(set.TryResolveType(consumer.Reader, typeReference, out _, out _, out var typeFailure));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, typeFailure);
        Assert.False(set.TryResolveMember(consumer.Reader, memberReference, out _, out _, out var memberFailure));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, memberFailure);

        set.Add(dependency.Reader);

        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var typeReader, out _));
        Assert.True(set.TryResolveMember(consumer.Reader, memberReference, out var memberReader, out _));
        Assert.Same(dependency.Reader, typeReader);
        Assert.Same(dependency.Reader, memberReader);
    }

    [Fact]
    public void Add_ReplacingFullIdentityInvalidatesWarmedTypeMemberAndReferenceResults()
    {
        using var workspace = new TemporaryDirectory();
        var firstDirectory = workspace.CreateDirectory("first");
        var secondDirectory = workspace.CreateDirectory("second");
        var firstPath = TestAssemblyFiles.BuildAssemblyFile(firstDirectory, "Dependency",
            "namespace Fixture; public sealed class Dependency { public int Value; public int Echo(int value) => value; }");
        var secondPath = TestAssemblyFiles.BuildAssemblyFile(secondDirectory, "Dependency",
            "namespace Fixture; public sealed class Dependency { public int Value; public int Echo(int value) => value + 1; }");
        using var first = new MetadataScope(firstPath);
        using var second = new MetadataScope(secondPath);
        using var consumer = new TestAssemblyBuilder("Consumer")
            .WithReference(firstPath)
            .WithSource("namespace Fixture; public sealed class Consumer { public int Run(Dependency value) => value.Echo(value.Value); }")
            .BuildMetadataScope();
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "Dependency");
        var methodReference = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "Dependency", "Echo");
        var fieldReference = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "Dependency", "Value");
        using var set = new AssemblySet();
        set.Add(first.Reader);
        var index = new MemberResolutionIndex();

        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var oldTypeReader, out _));
        Assert.True(set.TryResolveMember(consumer.Reader, methodReference, out var oldMethodReader, out _));
        Assert.True(set.TryResolveMember(consumer.Reader, fieldReference, index, out var oldFieldReader, out _));
        Assert.Same(first.Reader, oldTypeReader);
        Assert.Same(first.Reader, oldMethodReader);
        Assert.Same(first.Reader, oldFieldReader);

        set.Add(second.Reader);

        Assert.Same(second.Reader, Assert.Single(set.RegisteredAssemblies));
        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var newTypeReader, out _));
        Assert.True(set.TryResolveMember(consumer.Reader, methodReference, out var newMethodReader, out _));
        Assert.True(set.TryResolveMember(consumer.Reader, fieldReference, index, out var newFieldReader, out _));
        Assert.Same(second.Reader, newTypeReader);
        Assert.Same(second.Reader, newMethodReader);
        Assert.Same(second.Reader, newFieldReader);

        set.Dispose();
        set.Dispose();
        Assert.Equal("Dependency", second.Reader.GetString(second.Reader.GetAssemblyDefinition().Name));
    }

    [Fact]
    public void LazyAdd_DuringTypeResolutionKeepsTheWarmedResultCache()
    {
        using var workspace = new TemporaryDirectory();
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Dependency",
            "namespace Fixture; public sealed class Dependency { public int Echo(int value) => value; }");
        using var dependency = new MetadataScope(dependencyPath);
        using var consumer = new TestAssemblyBuilder("Consumer")
            .WithReference(dependencyPath)
            .WithSource("namespace Fixture; public sealed class Consumer { public int Run(Dependency value) => value.Echo(42); }")
            .BuildMetadataScope();
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "Dependency");
        var memberReference = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "Dependency", "Echo");
        var resolver = new CountingResolver(dependency.Reader);
        using var set = new AssemblySet(resolver);

        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var resolvedReader, out _));
        Assert.Same(dependency.Reader, resolvedReader);
        Assert.Equal(1, resolver.Calls);
        long immediateBefore = GC.GetAllocatedBytesForCurrentThread();
        bool immediateResolved = set.TryResolveType(consumer.Reader, typeReference, out var immediateReader, out _);
        long immediateAllocations = GC.GetAllocatedBytesForCurrentThread() - immediateBefore;
        Assert.True(immediateResolved);
        Assert.Same(dependency.Reader, immediateReader);
        Assert.Equal(0L, immediateAllocations);
        Assert.True(set.TryResolveMember(consumer.Reader, memberReference, out var memberReader, out _));
        Assert.Same(dependency.Reader, memberReader);
        Assert.Equal(1, resolver.Calls);

        bool allResolved = true;
        for (int i = 0; i < 32; i++)
        {
            allResolved &= set.TryResolveType(consumer.Reader, typeReference, out _, out _);
            allResolved &= set.TryResolveMember(consumer.Reader, memberReference, out _, out _);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            allResolved &= set.TryResolveType(consumer.Reader, typeReference, out _, out _);
            allResolved &= set.TryResolveMember(consumer.Reader, memberReference, out _, out _);
        }

        long allocations = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allResolved);
        Assert.Equal(1, resolver.Calls);
        Assert.Equal(0L, allocations);
    }

    [Fact]
    public void Add_MultiHopForwarderFailureThenSuccessAndReplacementUseCurrentReader()
    {
        using var fixture = new ForwarderFixture();
        var resolver = new MappingResolver();
        resolver.SetReader("Middle", fixture.Middle.Reader);
        using var set = new AssemblySet(resolver);
        set.Add(fixture.OuterForwarder.Reader);

        Assert.False(set.TryResolveType(fixture.Consumer.Reader, fixture.TypeReference, out _, out _, out var failure));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, failure);

        set.Add(fixture.FirstFinal.Reader);
        Assert.True(set.TryResolveType(fixture.Consumer.Reader, fixture.TypeReference, out var firstReader, out _));
        Assert.Same(fixture.FirstFinal.Reader, firstReader);
        Assert.True(set.TryResolveMember(fixture.Consumer.Reader, fixture.MemberReference, out var firstMemberReader, out _));
        Assert.Same(fixture.FirstFinal.Reader, firstMemberReader);

        set.Add(fixture.SecondFinal.Reader);
        Assert.True(set.TryResolveType(fixture.Consumer.Reader, fixture.TypeReference, out var secondReader, out _));
        Assert.True(set.TryResolveMember(fixture.Consumer.Reader, fixture.MemberReference, out var secondMemberReader, out _));
        Assert.Same(fixture.SecondFinal.Reader, secondReader);
        Assert.Same(fixture.SecondFinal.Reader, secondMemberReader);
    }

    [Fact]
    public void LazyAdd_MultiHopForwarderCachesTheImmediateNextQuery()
    {
        using var fixture = new ForwarderFixture();
        var resolver = new MappingResolver();
        resolver.SetReader("Middle", fixture.Middle.Reader);
        resolver.SetReader("Final", fixture.FirstFinal.Reader);
        using var set = new AssemblySet(resolver);
        set.Add(fixture.OuterForwarder.Reader);

        Assert.True(set.TryResolveType(fixture.Consumer.Reader, fixture.TypeReference, out var firstReader, out _));
        Assert.Same(fixture.FirstFinal.Reader, firstReader);
        Assert.Equal(2, resolver.Calls);

        long immediateBefore = GC.GetAllocatedBytesForCurrentThread();
        bool immediateResolved = set.TryResolveType(fixture.Consumer.Reader, fixture.TypeReference, out var cachedReader, out _);
        long immediateAllocations = GC.GetAllocatedBytesForCurrentThread() - immediateBefore;
        Assert.True(immediateResolved);
        Assert.Same(fixture.FirstFinal.Reader, cachedReader);
        Assert.Equal(2, resolver.Calls);
        Assert.Equal(0L, immediateAllocations);
        Assert.True(set.TryResolveMember(fixture.Consumer.Reader, fixture.MemberReference, out var memberReader, out _));
        Assert.Same(fixture.FirstFinal.Reader, memberReader);
        Assert.Equal(2, resolver.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Add_ReplacingSelectedForwarderDuringMemberLookupDoesNotCacheItsOldTarget(bool indexed, bool field)
    {
        using var fixture = new ForwarderFixture();
        var resolver = new MappingResolver();
        resolver.SetReader("Middle", fixture.Middle.Reader);
        resolver.SetReader("Final", fixture.FirstFinal.Reader);
        using var set = new AssemblySet(resolver);
        set.Add(fixture.OuterForwarder.Reader);
        resolver.OnResolve = name =>
        {
            if (name == "Middle")
            {
                set.Add(fixture.OuterReplacement.Reader);
            }
        };

        var memberReference = field ? fixture.FieldReference : fixture.MemberReference;
        var index = new MemberResolutionIndex();
        MetadataReader inFlightReader;
        EntityHandle inFlightHandle;
        bool inFlightResolved = indexed
            ? set.TryResolveMember(fixture.Consumer.Reader, memberReference, index, out inFlightReader, out inFlightHandle)
            : set.TryResolveMember(fixture.Consumer.Reader, memberReference, out inFlightReader, out inFlightHandle);
        Assert.True(inFlightResolved);
        Assert.Same(fixture.FirstFinal.Reader, inFlightReader);
        Assert.Equal(field ? HandleKind.FieldDefinition : HandleKind.MethodDefinition, inFlightHandle.Kind);
        Assert.Same(fixture.OuterReplacement.Reader, Assert.Single(set.RegisteredAssemblies,
            reader => reader.GetString(reader.GetAssemblyDefinition().Name) == "Outer"));

        MetadataReader currentReader;
        EntityHandle currentHandle;
        bool currentResolved = indexed
            ? set.TryResolveMember(fixture.Consumer.Reader, memberReference, index, out currentReader, out currentHandle)
            : set.TryResolveMember(fixture.Consumer.Reader, memberReference, out currentReader, out currentHandle);
        Assert.True(currentResolved);
        Assert.Same(fixture.OuterReplacement.Reader, currentReader);
        Assert.Equal(field ? HandleKind.FieldDefinition : HandleKind.MethodDefinition, currentHandle.Kind);
        Assert.True(set.TryResolveType(fixture.Consumer.Reader, fixture.TypeReference, out var currentTypeReader, out _));
        Assert.Same(fixture.OuterReplacement.Reader, currentTypeReader);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_InvalidatesWarmedModuleBinding(bool refreshExistingReader)
    {
        using var workspace = new TemporaryDirectory();
        var firstDirectory = workspace.CreateDirectory("first");
        var secondDirectory = workspace.CreateDirectory("second");
        var firstModulePath = TestAssemblyFiles.BuildModuleFile(firstDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var secondModulePath = TestAssemblyFiles.BuildModuleFile(secondDirectory, "Shared",
            "namespace Fixture; public sealed class SharedType { }");
        var hostPath = Path.Combine(firstDirectory, "Host.dll");
        using (var stream = new TestAssemblyBuilder("Host")
            .WithModuleReference(firstModulePath)
            .WithSource("namespace Fixture; public sealed class Host { public SharedType? Value => null; }")
            .BuildPEStream())
        {
            File.WriteAllBytes(hostPath, stream.ToArray());
        }

        var unrelatedPath = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "Unrelated",
            "namespace Fixture; public sealed class Unrelated { }");
        using var loader = new AssemblyLoader();
        var hostReader = loader.LoadFile(hostPath);
        var firstModuleReader = loader.LoadFile(firstModulePath);
        var secondModuleReader = loader.LoadFile(secondModulePath);
        var resolver = new SwitchingModuleResolver(firstModuleReader);
        using var set = new AssemblySet(resolver: null, moduleResolver: resolver);
        set.Add(hostReader);
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(hostReader, "Fixture", "SharedType");

        Assert.True(set.TryResolveType(hostReader, typeReference, out var firstReader, out _));
        Assert.Same(firstModuleReader, firstReader);
        resolver.Current = secondModuleReader;
        set.Add(refreshExistingReader ? hostReader : loader.LoadFile(unrelatedPath));

        Assert.True(set.TryResolveType(hostReader, typeReference, out var secondReader, out _));
        Assert.Same(secondModuleReader, secondReader);
        Assert.Equal(2, resolver.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Add_RefreshingHostDuringModuleResolutionDoesNotCacheOldMember(bool indexed, bool field)
    {
        using var workspace = new TemporaryDirectory();
        var firstDirectory = workspace.CreateDirectory("first");
        var secondDirectory = workspace.CreateDirectory("second");
        var moduleSource = "namespace Fixture; public sealed class SharedType { public int Value; public int Echo(int value) => value; }";
        var firstModulePath = TestAssemblyFiles.BuildModuleFile(firstDirectory, "Shared", moduleSource);
        var secondModulePath = TestAssemblyFiles.BuildModuleFile(secondDirectory, "Shared", moduleSource);
        using var loader = new AssemblyLoader();
        var firstModuleReader = loader.LoadFile(firstModulePath);
        var secondModuleReader = loader.LoadFile(secondModulePath);
        using var host = new TestAssemblyBuilder("Host")
            .WithModuleReference(firstModulePath)
            .WithSource("namespace Fixture; public sealed class Host { public int Run(SharedType value) { value.Value = value.Echo(42); return value.Value; } }")
            .BuildMetadataScope();
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(host.Reader, "Fixture", "SharedType");
        var memberReference = ResolutionTestHelpers.FindMemberReferenceHandle(host.Reader, "Fixture", "SharedType",
            field ? "Value" : "Echo");
        var resolver = new SwitchingModuleResolver(firstModuleReader);
        using var set = new AssemblySet(resolver: null, moduleResolver: resolver);
        set.Add(host.Reader);
        resolver.OnResolve = () =>
        {
            resolver.OnResolve = null;
            resolver.Current = secondModuleReader;
            set.Add(host.Reader);
        };

        var index = new MemberResolutionIndex();
        MetadataReader inFlightReader;
        EntityHandle inFlightHandle;
        bool inFlightResolved = indexed
            ? set.TryResolveMember(host.Reader, memberReference, index, out inFlightReader, out inFlightHandle)
            : set.TryResolveMember(host.Reader, memberReference, out inFlightReader, out inFlightHandle);
        Assert.True(inFlightResolved);
        Assert.Same(firstModuleReader, inFlightReader);
        Assert.Equal(field ? HandleKind.FieldDefinition : HandleKind.MethodDefinition, inFlightHandle.Kind);

        MetadataReader currentReader;
        EntityHandle currentHandle;
        bool currentResolved = indexed
            ? set.TryResolveMember(host.Reader, memberReference, index, out currentReader, out currentHandle)
            : set.TryResolveMember(host.Reader, memberReference, out currentReader, out currentHandle);
        Assert.True(currentResolved);
        Assert.Same(secondModuleReader, currentReader);
        Assert.Equal(field ? HandleKind.FieldDefinition : HandleKind.MethodDefinition, currentHandle.Kind);
        Assert.True(set.TryResolveType(host.Reader, typeReference, out var currentTypeReader, out _));
        Assert.Same(secondModuleReader, currentTypeReader);
        Assert.Equal(2, resolver.Calls);
    }

    [Fact]
    public void Add_PreservesCompatibleRegistrationOrderAndSubstitutionHistory()
    {
        using var workspace = new TemporaryDirectory();
        var versionOneDirectory = workspace.CreateDirectory("v1");
        var firstVersionTwoDirectory = workspace.CreateDirectory("v2a");
        var secondVersionTwoDirectory = workspace.CreateDirectory("v2b");
        var versionThreeDirectory = workspace.CreateDirectory("v3");
        var source = "using System.Reflection; [assembly: AssemblyVersion(\"{0}\")] namespace Fixture; public sealed class Dependency { }";
        var versionOnePath = TestAssemblyFiles.BuildAssemblyFile(versionOneDirectory, "Dependency",
            source.Replace("{0}", "1.0.0.0", StringComparison.Ordinal));
        var firstVersionTwoPath = TestAssemblyFiles.BuildAssemblyFile(firstVersionTwoDirectory, "Dependency",
            source.Replace("{0}", "2.0.0.0", StringComparison.Ordinal));
        var secondVersionTwoPath = TestAssemblyFiles.BuildAssemblyFile(secondVersionTwoDirectory, "Dependency",
            source.Replace("{0}", "2.0.0.0", StringComparison.Ordinal));
        var versionThreePath = TestAssemblyFiles.BuildAssemblyFile(versionThreeDirectory, "Dependency",
            source.Replace("{0}", "3.0.0.0", StringComparison.Ordinal));
        using var versionOne = new MetadataScope(versionOnePath);
        using var firstVersionTwo = new MetadataScope(firstVersionTwoPath);
        using var secondVersionTwo = new MetadataScope(secondVersionTwoPath);
        using var versionThree = new MetadataScope(versionThreePath);
        using var consumer = new TestAssemblyBuilder("Consumer")
            .WithReference(versionOnePath)
            .WithSource("namespace Fixture; public sealed class Consumer { public Dependency? Value => null; }")
            .BuildMetadataScope();
        var typeReference = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "Dependency");
        using var set = new AssemblySet(AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(firstVersionTwo.Reader);
        set.Add(versionThree.Reader);

        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var firstReader, out _));
        Assert.Same(firstVersionTwo.Reader, firstReader);
        Assert.Single(set.BindingSubstitutions);

        set.Add(secondVersionTwo.Reader);
        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var replacementReader, out _));
        Assert.Same(secondVersionTwo.Reader, replacementReader);
        Assert.Single(set.BindingSubstitutions);

        set.Add(versionOne.Reader);
        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var exactReader, out _));
        Assert.Same(versionOne.Reader, exactReader);
        Assert.Equal(AssemblyBindingPolicy.ExactThenCompatible, set.BindingPolicy);
        Assert.Equal(3, set.RegisteredAssemblies.Count);
        Assert.Single(set.BindingSubstitutions);

        set.ClearCaches();
        Assert.Single(set.BindingSubstitutions);
        Assert.True(set.TryResolveType(consumer.Reader, typeReference, out var afterClearReader, out _));
        Assert.Same(versionOne.Reader, afterClearReader);
    }

    private sealed class ForwarderFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace = new();

        public ForwarderFixture()
        {
            var replacementDirectory = _workspace.CreateDirectory("replacement");
            var outerDirectory = _workspace.CreateDirectory("outer");
            var middleDirectory = _workspace.CreateDirectory("middle");
            var firstFinalDirectory = _workspace.CreateDirectory("firstFinal");
            var secondFinalDirectory = _workspace.CreateDirectory("secondFinal");
            var replacementPath = TestAssemblyFiles.BuildAssemblyFile(replacementDirectory, "Outer",
                "using System.Reflection; [assembly: AssemblyVersion(\"1.0.0.0\")] namespace Fixture; public sealed class ForwardedType { public int Value; public int Echo(int value) => value; }");
            var firstFinalPath = TestAssemblyFiles.BuildAssemblyFile(firstFinalDirectory, "Final",
                "using System.Reflection; [assembly: AssemblyVersion(\"1.0.0.0\")] namespace Fixture; public sealed class ForwardedType { public int Value; public int Echo(int value) => value; }");
            var secondFinalPath = TestAssemblyFiles.BuildAssemblyFile(secondFinalDirectory, "Final",
                "using System.Reflection; [assembly: AssemblyVersion(\"1.0.0.0\")] namespace Fixture; public sealed class ForwardedType { public int Value; public int Echo(int value) => value + 1; }");
            var middlePath = BuildForwarderFile(middleDirectory, "Middle", "Final");
            var outerPath = BuildForwarderFile(outerDirectory, "Outer", "Middle");
            OuterReplacement = new MetadataScope(replacementPath);
            OuterForwarder = new MetadataScope(outerPath);
            Middle = new MetadataScope(middlePath);
            FirstFinal = new MetadataScope(firstFinalPath);
            SecondFinal = new MetadataScope(secondFinalPath);
            Consumer = new TestAssemblyBuilder("Consumer")
                .WithReference(replacementPath)
                .WithSource("namespace Fixture; public sealed class Consumer { public int Run(ForwardedType value) { value.Value = value.Echo(42); return value.Value; } }")
                .BuildMetadataScope();
            TypeReference = ResolutionTestHelpers.FindTypeReferenceHandle(Consumer.Reader, "Fixture", "ForwardedType");
            MemberReference = ResolutionTestHelpers.FindMemberReferenceHandle(Consumer.Reader, "Fixture", "ForwardedType", "Echo");
            FieldReference = ResolutionTestHelpers.FindMemberReferenceHandle(Consumer.Reader, "Fixture", "ForwardedType", "Value");
        }

        public MetadataScope Consumer { get; }
        public MetadataScope OuterForwarder { get; }
        public MetadataScope OuterReplacement { get; }
        public MetadataScope Middle { get; }
        public MetadataScope FirstFinal { get; }
        public MetadataScope SecondFinal { get; }
        public TypeReferenceHandle TypeReference { get; }
        public MemberReferenceHandle MemberReference { get; }
        public MemberReferenceHandle FieldReference { get; }

        public void Dispose()
        {
            Consumer.Dispose();
            OuterForwarder.Dispose();
            OuterReplacement.Dispose();
            Middle.Dispose();
            FirstFinal.Dispose();
            SecondFinal.Dispose();
            _workspace.Dispose();
        }
    }

    private static string BuildForwarderFile(string directory, string assemblyName, string targetAssemblyName)
    {
        var path = Path.Combine(directory, assemblyName + ".dll");
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(assemblyName + ".dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(assemblyName), new Version(1, 0, 0, 0), default, default,
            0, AssemblyHashAlgorithm.None);
        var targetReference = metadata.AddAssemblyReference(metadata.GetOrAddString(targetAssemblyName),
            new Version(1, 0, 0, 0), default, default, 0, default);
        metadata.AddExportedType(TypeAttributes.Public | ForwarderTypeAttribute,
            metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("ForwardedType"), targetReference, 1);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata),
            new BlobBuilder(), flags: CorFlags.ILOnly);
        var image = new BlobBuilder();
        pe.Serialize(image);
        File.WriteAllBytes(path, image.ToArray());
        return path;
    }

    private sealed class MappingResolver : IAssemblyReferenceResolver
    {
        private readonly Dictionary<string, MetadataReader> _readers = new(StringComparer.OrdinalIgnoreCase);

        public int Calls { get; private set; }
        public Action<string>? OnResolve { get; set; }

        public void SetReader(string name, MetadataReader reader)
        {
            _readers[name] = reader;
        }

        public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            Calls++;
            var name = requestingReader.GetString(requestingReader.GetAssemblyReference(reference).Name);
            OnResolve?.Invoke(name);
            return _readers.TryGetValue(name, out resolvedReader);
        }
    }

    private sealed class SwitchingModuleResolver(MetadataReader reader) : IModuleResolver
    {
        public MetadataReader Current { get; set; } = reader;
        public int Calls { get; private set; }
        public Action? OnResolve { get; set; }

        public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            Calls++;
            resolvedReader = Current;
            OnResolve?.Invoke();
            return true;
        }
    }

    private sealed class CountingResolver(MetadataReader reader) : IAssemblyReferenceResolver
    {
        public int Calls { get; private set; }

        public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            Calls++;
            resolvedReader = reader;
            return true;
        }
    }
}

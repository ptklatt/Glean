using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblySetMemberHierarchyTests
{
    public static IEnumerable<object[]> LookupModes()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            yield return [mode, false];
            yield return [mode, true];
        }
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_HiddenDifferentSignature_ContinuesToNearestMatchingBase(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var far = graph.Type("Far");
        var near = graph.Type("Near", far.Handle);
        var owner = graph.Type("Owner", near.Handle);
        graph.Member(far, field, [0x08]);
        graph.Member(near, field, [0x08]);
        graph.Member(owner, field, [0x0e]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out _));
        Assert.Equal("Near", DeclaringType(reader, handle));
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_OwnerMatches_DoesNotSearchMissingBase(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var missing = graph.TypeReference("Missing", "Base");
        var owner = graph.Type("Owner", missing);
        graph.Member(owner, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out _));
        Assert.Equal("Owner", DeclaringType(reader, handle));
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_AmbiguousOwner_DoesNotFallBackToUniqueBase(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var parent = graph.Type("Base");
        var owner = graph.Type("Owner", parent.Handle);
        graph.Member(parent, field, [0x08]);
        graph.Member(owner, field, [0x08]);
        graph.Member(owner, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.False(Resolve(set, scope.Reader, reference, mode, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberAmbiguous, reason);
    }

    [Theory]
    [InlineData(0, ".ctor")]
    [InlineData(1, ".ctor")]
    [InlineData(2, ".ctor")]
    [InlineData(0, ".cctor")]
    [InlineData(1, ".cctor")]
    [InlineData(2, ".cctor")]
    public void TryResolveMember_InitializerExistsOnlyOnBase_IsNotInherited(int mode, string name)
    {
        var graph = new MetadataGraph("Library");
        var parent = graph.Type("Base");
        var owner = graph.Type("Owner", parent.Handle);
        graph.Member(parent, false, [0x08], name);
        var reference = graph.Reference(owner.Handle, false, [0x08], name);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.False(Resolve(set, scope.Reader, reference, mode, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberNotFound, reason);
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_InterfaceDiamond_ResolvesCommonAncestorOnce(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var common = graph.Type("ICommon", isInterface: true);
        var left = graph.Type("ILeft", isInterface: true);
        var right = graph.Type("IRight", isInterface: true);
        var owner = graph.Type("IOwner", isInterface: true);
        left.Interfaces.Add(common.Handle);
        right.Interfaces.Add(common.Handle);
        owner.Interfaces.Add(left.Handle);
        owner.Interfaces.Add(right.Handle);
        graph.Member(common, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out _));
        Assert.Equal("ICommon", DeclaringType(reader, handle));
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_InterfacesMatchAtSameLevel_RemainsAmbiguousOnRepeatedLookup(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var left = graph.Type("ILeft", isInterface: true);
        var right = graph.Type("IRight", isInterface: true);
        var owner = graph.Type("IOwner", isInterface: true);
        owner.Interfaces.Add(left.Handle);
        owner.Interfaces.Add(right.Handle);
        graph.Member(left, field, [0x08]);
        graph.Member(right, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        for (int i = 0; i < 2; i++)
        {
            Assert.False(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out var reason));
            Assert.Equal(ResolutionFailureReason.MemberAmbiguous, reason);
            Assert.Null(reader);
            Assert.True(handle.IsNil);
        }
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_NearInterfaceMatches_DoesNotSearchDeeperLevel(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var deep = graph.Type("IDeep", isInterface: true);
        var near = graph.Type("INear", isInterface: true);
        var branch = graph.Type("IBranch", isInterface: true);
        var owner = graph.Type("IOwner", isInterface: true);
        branch.Interfaces.Add(deep.Handle);
        owner.Interfaces.Add(branch.Handle);
        owner.Interfaces.Add(near.Handle);
        graph.Member(deep, field, [0x08]);
        graph.Member(near, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out _));
        Assert.Equal("INear", DeclaringType(reader, handle));
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_ClassOwnsReference_DoesNotSearchImplementedInterfaces(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var contract = graph.Type("IContract", isInterface: true);
        var owner = graph.Type("Owner");
        owner.Interfaces.Add(contract.Handle);
        graph.Member(contract, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.False(Resolve(set, scope.Reader, reference, mode, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberNotFound, reason);
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_GenericBaseEdges_ComposeFormalTypeArguments(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var parent = graph.Type("Base`1", genericCount: 1);
        var middle = graph.Type("Middle`1", genericCount: 1);
        var owner = graph.Type("Owner`2", genericCount: 2);
        middle.Base = graph.Specification(Instantiate(parent.Handle, [0x1d, 0x13, 0x00]));
        owner.Base = graph.Specification(Instantiate(middle.Handle, [0x13, 0x01]));
        graph.Member(parent, field, [0x13, 0x00]);
        graph.Member(parent, field, [0x0e]);
        var closedOwner = graph.Specification(Instantiate(owner.Handle, [0x0e], [0x0e]));
        var reference = graph.Reference(closedOwner, field, [0x1d, 0x13, 0x01]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out _));
        Assert.Equal("Base`1", DeclaringType(reader, handle));
        Assert.Equal(1, MetadataTokens.GetRowNumber(handle));
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_ClosedGenericOwner_DoesNotCollapseDistinctFormalOverloads(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var owner = graph.Type("Owner`2", genericCount: 2);
        graph.Member(owner, field, [0x13, 0x00]);
        graph.Member(owner, field, [0x13, 0x01]);
        graph.Member(owner, field, [0x0e]);
        var closedOwner = graph.Specification(Instantiate(owner.Handle, [0x0e], [0x0e]));
        var reference = graph.Reference(closedOwner, field, [0x13, 0x01]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out _, out var handle, out _));
        Assert.Equal(2, MetadataTokens.GetRowNumber(handle));
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_SameGenericInterfaceWithDifferentArguments_SearchesBothInstantiations(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var parent = graph.Type("IBase`1", isInterface: true, genericCount: 1);
        var owner = graph.Type("IOwner", isInterface: true);
        owner.Interfaces.Add(graph.Specification(Instantiate(parent.Handle, [0x08])));
        owner.Interfaces.Add(graph.Specification(Instantiate(parent.Handle, [0x0e])));
        graph.Member(parent, field, [0x13, 0x00]);
        var reference = graph.Reference(owner.Handle, field, [0x0e]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out _));
        Assert.Equal("IBase`1", DeclaringType(reader, handle));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void TryResolveMember_CyclicGenericInheritance_TerminatesWithoutFindingMissingMember(int mode, bool isInterface)
    {
        var graph = new MetadataGraph("Library");
        var owner = graph.Type("Owner`1", isInterface: isInterface, genericCount: 1);
        var parent = graph.Type("Parent`1", isInterface: isInterface, genericCount: 1);
        var expandingOwner = graph.Specification(Instantiate(owner.Handle, [0x1d, 0x13, 0x00]));
        var genericParent = graph.Specification(Instantiate(parent.Handle, [0x13, 0x00]));
        if (isInterface)
        {
            owner.Interfaces.Add(genericParent);
            parent.Interfaces.Add(expandingOwner);
        }
        else
        {
            owner.Base = genericParent;
            parent.Base = expandingOwner;
        }

        var reference = graph.Reference(owner.Handle, false, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.False(Resolve(set, scope.Reader, reference, mode, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberNotFound, reason);
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_ReplacingCrossAssemblyBase_RefreshesInheritedResult(int mode, bool field)
    {
        var firstGraph = new MetadataGraph("BaseLibrary");
        var firstBase = firstGraph.Type("Base");
        firstGraph.Member(firstBase, field, [0x08]);
        using var first = firstGraph.Build();
        var secondGraph = new MetadataGraph("BaseLibrary");
        var secondBase = secondGraph.Type("Base");
        secondGraph.Member(secondBase, field, [0x08]);
        using var second = secondGraph.Build();
        var graph = new MetadataGraph("Library");
        var baseReference = graph.TypeReference("BaseLibrary", "Base");
        var owner = graph.Type("Owner", baseReference);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(first.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var firstReader, out _, out _));
        Assert.Same(first.Reader, firstReader);
        set.Add(second.Reader);
        Assert.True(Resolve(set, scope.Reader, reference, mode, out var secondReader, out _, out _));
        Assert.Same(second.Reader, secondReader);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TryResolveMember_WarmedInheritedIndexedLookup_DoesNotAllocate(bool cacheSignatures, bool field)
    {
        var graph = new MetadataGraph("Library");
        var parent = graph.Type("Base");
        var owner = graph.Type("Owner", parent.Handle);
        graph.Member(parent, field, [0x08]);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);
        var index = new MemberResolutionIndex(cacheSignatures);
        Assert.True(set.TryResolveMember(scope.Reader, reference, index, out _, out _));
        bool resolved = true;
        for (int i = 0; i < 32; i++)
        {
            resolved &= set.TryResolveMember(scope.Reader, reference, index, out _, out _);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            resolved &= set.TryResolveMember(scope.Reader, reference, index, out _, out _);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(resolved);
        Assert.Equal(0L, allocated);
    }

    public static IEnumerable<object[]> DifferentScopes()
    {
        foreach (var mode in LookupModes())
        {
            for (int identityPart = 0; identityPart < 3; identityPart++)
            {
                yield return [mode[0], mode[1], identityPart];
            }
        }
    }

    [Theory]
    [MemberData(nameof(DifferentScopes))]
    public void TryResolveMember_NamedTypesDifferByFullAssemblyIdentity_StayDistinct(int mode, bool field, int identityPart)
    {
        var graph = new MetadataGraph("Library");
        var owner = graph.Type("Owner");
        var wrong = graph.TypeReference("Types", "Marker", identityPart == 0 ? 2 : 1,
            identityPart == 1 ? "fr" : "", identityPart == 2 ? [1, 2, 3, 4, 5, 6, 7, 8] : null);
        var expected = graph.TypeReference("Types", "Marker");
        graph.Member(owner, field, Named(wrong));
        graph.Member(owner, field, Named(expected));
        var reference = graph.Reference(owner.Handle, field, Named(expected));
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out _, out var handle, out _));
        Assert.Equal(2, MetadataTokens.GetRowNumber(handle));
    }

    [Theory]
    [MemberData(nameof(DifferentScopes))]
    public void TryResolveMember_NamedTypeVersionDiffers_UsesActualConfiguredBinding(int mode, bool field, int scenario)
    {
        var graph = new MetadataGraph("Library");
        var owner = graph.Type("Owner");
        var definitionType = graph.TypeReference("Types", "Marker", 2);
        var referenceType = graph.TypeReference("Types", "Marker");
        graph.Member(owner, field, Named(definitionType));
        var reference = graph.Reference(owner.Handle, field, Named(referenceType));
        using var scope = graph.Build();
        var versionTwoGraph = new MetadataGraph("Types", 2);
        versionTwoGraph.Type("Marker");
        using var versionTwo = versionTwoGraph.Build();
        var versionOneGraph = new MetadataGraph("Types");
        versionOneGraph.Type("Marker");
        using var versionOne = versionOneGraph.Build();
        using var set = new AssemblySet(scenario == 0 ? AssemblyBindingPolicy.Exact : AssemblyBindingPolicy.ExactThenCompatible);
        set.Add(scope.Reader);
        set.Add(versionTwo.Reader);
        if (scenario == 2)
        {
            set.Add(versionOne.Reader);
        }

        bool resolved = Resolve(set, scope.Reader, reference, mode, out _, out _, out var reason);
        Assert.Equal(scenario == 1, resolved);
        Assert.Equal(scenario == 1 ? ResolutionFailureReason.None : ResolutionFailureReason.MemberNotFound, reason);
        Assert.Equal(scenario == 1 ? 1 : 0, set.BindingSubstitutions.Count);
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_NamedTypeIsForwarded_MatchesResolvedDefinition(int mode, bool field)
    {
        var actualGraph = new MetadataGraph("Actual");
        actualGraph.Type("Marker");
        using var actual = actualGraph.Build();
        var facadeGraph = new MetadataGraph("Facade");
        facadeGraph.Forward("Marker", "Actual");
        using var facade = facadeGraph.Build();
        var graph = new MetadataGraph("Library");
        var owner = graph.Type("Owner");
        var definitionType = graph.TypeReference("Actual", "Marker");
        var referenceType = graph.TypeReference("Facade", "Marker");
        graph.Member(owner, field, Named(definitionType));
        var reference = graph.Reference(owner.Handle, field, Named(referenceType));
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(actual.Reader);
        set.Add(facade.Reader);
        set.Add(scope.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out var reader, out _, out _));
        Assert.Same(scope.Reader, reader);
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_UnavailableBase_PreservesAssemblyFailure(int mode, bool field)
    {
        var graph = new MetadataGraph("Library");
        var missing = graph.TypeReference("Missing", "Base");
        var owner = graph.Type("Owner", missing);
        var reference = graph.Reference(owner.Handle, field, [0x08]);
        using var scope = graph.Build();
        using var set = new AssemblySet();
        set.Add(scope.Reader);

        Assert.False(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out var reason));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
        Assert.Null(reader);
        Assert.True(handle.IsNil);
    }

    public static IEnumerable<object[]> ModuleBindings()
    {
        foreach (var mode in LookupModes())
        {
            yield return [mode[0], mode[1], false];
            yield return [mode[0], mode[1], true];
        }
    }

    [Theory]
    [MemberData(nameof(ModuleBindings))]
    public void TryResolveMember_SameModuleNameInDifferentScopes_UsesResolvedModuleIdentity(int mode, bool field, bool sameModule)
    {
        var firstModuleGraph = new MetadataGraph("Shared", isAssembly: false);
        firstModuleGraph.Type("Marker");
        using var firstModule = firstModuleGraph.Build();
        var secondModuleGraph = new MetadataGraph("Shared", isAssembly: false);
        secondModuleGraph.Type("Marker");
        using var secondModule = secondModuleGraph.Build();
        var libraryGraph = new MetadataGraph("Library");
        var owner = libraryGraph.Type("Owner");
        var definitionType = libraryGraph.ModuleType("Shared.netmodule", "Marker");
        libraryGraph.Member(owner, field, Named(definitionType));
        using var library = libraryGraph.Build();
        var consumerGraph = new MetadataGraph("Consumer");
        var ownerReference = consumerGraph.TypeReference("Library", "Owner");
        var referenceType = consumerGraph.ModuleType("Shared.netmodule", "Marker");
        var reference = consumerGraph.Reference(ownerReference, field, Named(referenceType));
        using var consumer = consumerGraph.Build();
        var resolver = new ScopedModuleResolver(library.Reader, firstModule.Reader,
            sameModule ? firstModule.Reader : secondModule.Reader);
        using var set = new AssemblySet(resolver: null, moduleResolver: resolver);
        set.Add(library.Reader);

        Assert.Equal(sameModule, Resolve(set, consumer.Reader, reference, mode, out _, out _, out var reason));
        Assert.Equal(sameModule ? ResolutionFailureReason.None : ResolutionFailureReason.MemberNotFound, reason);
    }

    [Theory]
    [MemberData(nameof(LookupModes))]
    public void TryResolveMember_ResolverAddsEarlierSignatureCandidate_NextLookupReportsAmbiguity(int mode, bool field)
    {
        var actualGraph = new MetadataGraph("Actual");
        actualGraph.Type("Marker");
        using var actual = actualGraph.Build();
        var firstGraph = new MetadataGraph("FirstFacade");
        firstGraph.Forward("Marker", "Actual");
        using var first = firstGraph.Build();
        var secondGraph = new MetadataGraph("SecondFacade");
        secondGraph.Forward("Marker", "Actual");
        using var second = secondGraph.Build();
        var graph = new MetadataGraph("Library");
        var owner = graph.Type("Owner");
        var firstType = graph.TypeReference("FirstFacade", "Marker");
        var secondType = graph.TypeReference("SecondFacade", "Marker");
        var actualType = graph.TypeReference("Actual", "Marker");
        graph.Member(owner, field, Named(firstType));
        graph.Member(owner, field, Named(secondType));
        var reference = graph.Reference(owner.Handle, field, Named(actualType));
        using var scope = graph.Build();
        var resolver = new SideEffectResolver(second.Reader);
        using var set = new AssemblySet(resolver);
        set.Add(actual.Reader);
        set.Add(scope.Reader);
        resolver.OnResolve = () => set.Add(first.Reader);

        Assert.True(Resolve(set, scope.Reader, reference, mode, out _, out var firstHandle, out _));
        Assert.Equal(2, MetadataTokens.GetRowNumber(firstHandle));
        Assert.False(Resolve(set, scope.Reader, reference, mode, out var reader, out var handle, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberAmbiguous, reason);
        Assert.Null(reader);
        Assert.True(handle.IsNil);
    }

    private static bool Resolve(AssemblySet set, MetadataReader reader, MemberReferenceHandle reference,
        int mode, out MetadataReader targetReader, out EntityHandle targetHandle, out ResolutionFailureReason reason)
    {
        return mode == 0
            ? set.TryResolveMember(reader, reference, out targetReader, out targetHandle, out reason)
            : set.TryResolveMember(reader, reference, new MemberResolutionIndex(mode == 2),
                out targetReader, out targetHandle, out reason);
    }

    private static string DeclaringType(MetadataReader reader, EntityHandle handle)
    {
        var type = handle.Kind == HandleKind.FieldDefinition
            ? reader.GetFieldDefinition((FieldDefinitionHandle)handle).GetDeclaringType()
            : reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType();
        return reader.GetString(reader.GetTypeDefinition(type).Name);
    }

    private static byte[] Named(EntityHandle handle)
    {
        var blob = new BlobBuilder();
        blob.WriteByte(0x12);
        WriteTypeHandle(blob, handle);
        return blob.ToArray();
    }

    private static byte[] Instantiate(EntityHandle handle, params byte[][] arguments)
    {
        var blob = new BlobBuilder();
        blob.WriteByte(0x15);
        blob.WriteBytes(Named(handle));
        blob.WriteCompressedInteger(arguments.Length);
        foreach (var argument in arguments)
        {
            blob.WriteBytes(argument);
        }

        return blob.ToArray();
    }

    private static void WriteTypeHandle(BlobBuilder blob, EntityHandle handle)
    {
        int tag = handle.Kind == HandleKind.TypeDefinition ? 0 : 1;
        blob.WriteCompressedInteger((MetadataTokens.GetRowNumber(handle) << 2) | tag);
    }

    private sealed class MetadataGraph
    {
        private readonly MetadataBuilder _metadata = new();
        private readonly List<Node> _nodes = new();

        public MetadataGraph(string name, int version = 1, bool isAssembly = true)
        {
            _metadata.AddModule(0, _metadata.GetOrAddString(name + (isAssembly ? ".dll" : ".netmodule")),
                _metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
            if (isAssembly)
            {
                _metadata.AddAssembly(_metadata.GetOrAddString(name), new Version(version, 0, 0, 0),
                    default, default, 0, AssemblyHashAlgorithm.None);
            }
        }

        public Node Type(string name, EntityHandle parent = default, bool isInterface = false, int genericCount = 0)
        {
            var node = new Node(name, MetadataTokens.TypeDefinitionHandle(_nodes.Count + 2), parent, isInterface, genericCount);
            _nodes.Add(node);
            return node;
        }

        public TypeReferenceHandle TypeReference(string assembly, string name, int version = 1, string culture = "", byte[]? token = null)
        {
            var scope = _metadata.AddAssemblyReference(_metadata.GetOrAddString(assembly), new Version(version, 0, 0, 0),
                _metadata.GetOrAddString(culture), token == null ? default : _metadata.GetOrAddBlob(token), 0, default);
            return _metadata.AddTypeReference(scope, _metadata.GetOrAddString("Fixture"), _metadata.GetOrAddString(name));
        }

        public void Forward(string name, string assembly)
        {
            var scope = _metadata.AddAssemblyReference(_metadata.GetOrAddString(assembly), new Version(1, 0, 0, 0),
                default, default, 0, default);
            _metadata.AddExportedType(TypeAttributes.Public | (TypeAttributes)0x00200000,
                _metadata.GetOrAddString("Fixture"), _metadata.GetOrAddString(name), scope, 0);
        }

        public TypeReferenceHandle ModuleType(string module, string name)
        {
            var scope = _metadata.AddModuleReference(_metadata.GetOrAddString(module));
            return _metadata.AddTypeReference(scope, _metadata.GetOrAddString("Fixture"), _metadata.GetOrAddString(name));
        }

        public TypeSpecificationHandle Specification(byte[] signature) => _metadata.AddTypeSpecification(_metadata.GetOrAddBlob(signature));

        public void Member(Node node, bool field, byte[] type, string name = "Member")
        {
            (field ? node.Fields : node.Methods).Add((name, Wrap(type, field, name)));
        }

        public MemberReferenceHandle Reference(EntityHandle owner, bool field, byte[] type, string name = "Member")
        {
            return _metadata.AddMemberReference(owner, _metadata.GetOrAddString(name), _metadata.GetOrAddBlob(Wrap(type, field, name)));
        }

        public MetadataScope Build()
        {
            int fieldRow = 1;
            int methodRow = 1;
            _metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, _metadata.GetOrAddString("<Module>"),
                default, MetadataTokens.FieldDefinitionHandle(fieldRow), MetadataTokens.MethodDefinitionHandle(methodRow));
            foreach (var node in _nodes)
            {
                _metadata.AddTypeDefinition(TypeAttributes.Public | (node.IsInterface ? TypeAttributes.Interface | TypeAttributes.Abstract : 0),
                    _metadata.GetOrAddString("Fixture"), _metadata.GetOrAddString(node.Name), node.Base,
                    MetadataTokens.FieldDefinitionHandle(fieldRow), MetadataTokens.MethodDefinitionHandle(methodRow));
                foreach (var field in node.Fields)
                {
                    _metadata.AddFieldDefinition(FieldAttributes.Public, _metadata.GetOrAddString(field.Name), _metadata.GetOrAddBlob(field.Signature));
                    fieldRow++;
                }

                foreach (var method in node.Methods)
                {
                    var attributes = method.Name == ".ctor" ? MethodAttributes.Public : MethodAttributes.Public | MethodAttributes.Static;
                    if ((method.Name == ".ctor") || (method.Name == ".cctor"))
                    {
                        attributes |= MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                    }

                    _metadata.AddMethodDefinition(attributes, MethodImplAttributes.IL,
                        _metadata.GetOrAddString(method.Name), _metadata.GetOrAddBlob(method.Signature), -1, MetadataTokens.ParameterHandle(1));
                    methodRow++;
                }

                foreach (var parent in node.Interfaces)
                {
                    _metadata.AddInterfaceImplementation(node.Handle, parent);
                }

                for (int i = 0; i < node.GenericCount; i++)
                {
                    _metadata.AddGenericParameter(node.Handle, GenericParameterAttributes.None, _metadata.GetOrAddString("T" + i), i);
                }
            }

            var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(_metadata),
                new BlobBuilder(), flags: CorFlags.ILOnly);
            var image = new BlobBuilder();
            pe.Serialize(image);
            return new MetadataScope(new MemoryStream(image.ToArray(), writable: false));
        }

        private static byte[] Wrap(byte[] type, bool field, string name)
        {
            if (field)
            {
                return [0x06, .. type];
            }

            return name switch
            {
                ".ctor" => [0x20, 0x01, 0x01, .. type],
                ".cctor" => [0x00, 0x00, 0x01],
                _ => [0x00, 0x01, 0x01, .. type]
            };
        }
    }

    private sealed class Node(string name, TypeDefinitionHandle handle, EntityHandle parent, bool isInterface, int genericCount)
    {
        public string Name { get; } = name;
        public TypeDefinitionHandle Handle { get; } = handle;
        public EntityHandle Base { get; set; } = parent;
        public bool IsInterface { get; } = isInterface;
        public int GenericCount { get; } = genericCount;
        public List<EntityHandle> Interfaces { get; } = new();
        public List<(string Name, byte[] Signature)> Fields { get; } = new();
        public List<(string Name, byte[] Signature)> Methods { get; } = new();
    }

    private sealed class ScopedModuleResolver(MetadataReader library, MetadataReader first, MetadataReader second) : IModuleResolver
    {
        public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            resolvedReader = ReferenceEquals(requestingReader, library) ? first : second;
            return true;
        }
    }

    private sealed class SideEffectResolver(MetadataReader second) : IAssemblyReferenceResolver
    {
        public Action? OnResolve { get; set; }

        public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            var name = requestingReader.GetString(requestingReader.GetAssemblyReference(reference).Name);
            if (name != "SecondFacade")
            {
                resolvedReader = null;
                return false;
            }

            OnResolve?.Invoke();
            resolvedReader = second;
            return true;
        }
    }
}

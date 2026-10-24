using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Extensions;
using Glean.Resolution;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class ResolutionExtensionTests
{
    [Fact]
    public void TypeReferenceContextTryResolve_ResolvesReferencedType()
    {
        using var fixture = ResolutionExtensionFixture.Create();

        var typeReference = TypeReferenceContext.Create(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service"));

        Assert.True(typeReference.TryResolve(fixture.Set, out var resolved));
        Assert.Same(fixture.Dependency.Reader, resolved.Reader);
        Assert.Equal("Service", resolved.Name);
    }

    [Fact]
    public void TypeReferenceContextTryResolve_WithReason_PreservesFailureReason()
    {
        using var fixture = MissingResolutionFixture.Create();

        var typeReference = TypeReferenceContext.Create(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "Base"));

        Assert.False(typeReference.TryResolve(fixture.Set, out var resolved, out var reason));
        Assert.Equal(default, resolved);
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
    }

    [Fact]
    public void MemberReferenceContextTryResolve_ResolvesMethodAndField()
    {
        using var fixture = ResolutionExtensionFixture.Create();

        var methodReference = MemberReferenceContext.Create(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindMemberReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service", "Echo"));

        Assert.True(methodReference.TryResolve(fixture.Set, out var methodReader, out var methodHandle));
        Assert.Same(fixture.Dependency.Reader, methodReader);
        Assert.Equal(HandleKind.MethodDefinition, methodHandle.Kind);

        var fieldReference = MemberReferenceContext.Create(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindMemberReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service", "Value"));
        var index = new MemberResolutionIndex();

        Assert.True(fieldReference.TryResolve(fixture.Set, index, out var fieldReader, out var fieldHandle));
        Assert.Same(fixture.Dependency.Reader, fieldReader);
        Assert.Equal(HandleKind.FieldDefinition, fieldHandle.Kind);
    }

    [Fact]
    public void MemberReferenceContextTryResolve_WithReason_PreservesFailureReason()
    {
        using var fixture = MissingResolutionFixture.Create();

        var methodReference = MemberReferenceContext.Create(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindMemberReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service", "Echo"));

        Assert.False(methodReference.TryResolve(fixture.Set, out var methodReader, out var methodHandle, out var reason));
        Assert.Null(methodReader);
        Assert.Equal(default, methodHandle);
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
    }

    [Fact]
    public void TypeContextTryGetBaseType_ResolvesCrossAssemblyBaseTypes()
    {
        using var fixture = ResolutionExtensionFixture.Create();

        Assert.True(fixture.Derived.TryGetBaseType(fixture.Set, out var derivedBase));
        Assert.Same(fixture.Dependency.Reader, derivedBase.Reader);
        Assert.Equal("Middle", derivedBase.Name);

        Assert.True(fixture.GenericDerived.TryGetBaseType(fixture.Set, out var genericBase));
        Assert.Same(fixture.Dependency.Reader, genericBase.Reader);
        Assert.Equal("GenericBase`1", genericBase.Name);
    }

    [Fact]
    public void TypeContextTryGetBaseType_WithReason_PreservesFailureReason()
    {
        using var fixture = MissingResolutionFixture.Create();

        Assert.False(fixture.Derived.TryGetBaseType(fixture.Set, out var baseType, out var reason));
        Assert.Equal(default, baseType);
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
    }

    [Fact]
    public void EnumerateBaseTypes_AndIsSubclassOf_WalkCrossAssemblyChains()
    {
        using var fixture = ResolutionExtensionFixture.Create();

        var names = new List<string>();
        foreach (var type in fixture.Derived.EnumerateBaseTypes(fixture.Set))
        {
            names.Add(type.Name);
        }

        Assert.Equal(new[] { "Middle", "Base" }, names);
        Assert.True(fixture.Derived.IsSubclassOf("Contracts", "Base", fixture.Set));
        Assert.False(fixture.Derived.IsSubclassOf("Contracts", "GenericBase`1", fixture.Set));
    }

    [Fact]
    public void EnumerateAllInterfaces_ResolvesInheritedAndGenericInterfaces()
    {
        using var fixture = ResolutionExtensionFixture.Create();

        var derivedInterfaces = new List<string>();
        foreach (var type in fixture.Derived.EnumerateAllInterfaces(fixture.Set))
        {
            derivedInterfaces.Add(type.Name);
        }

        var genericDerivedInterfaces = new List<string>();
        foreach (var type in fixture.GenericDerived.EnumerateAllInterfaces(fixture.Set))
        {
            genericDerivedInterfaces.Add(type.Name);
        }

        Assert.Equal(new[] { "IRoot" }, derivedInterfaces);
        Assert.Equal(new[] { "IGeneric`1" }, genericDerivedInterfaces);
    }

    [Fact]
    public void TypeReferenceSignatureResolveIsValueType_ResolvesExternalStruct()
    {
        using var fixture = ResolutionExtensionFixture.Create();

        var structReference = new TypeReferenceSignature(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "ExternalStruct"));
        var serviceReference = new TypeReferenceSignature(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service"));

        var structIsValueType = structReference.ResolveIsValueType(fixture.Set);
        var serviceIsValueType = serviceReference.ResolveIsValueType(fixture.Set);

        Assert.True(structIsValueType.HasValue);
        Assert.True(structIsValueType.Value);
        Assert.True(serviceIsValueType.HasValue);
        Assert.False(serviceIsValueType.Value);
    }

    [Fact]
    public void TypeReferenceSignatureTryResolve_ResolvesDefinitionWithAndWithoutReason()
    {
        using var fixture = ResolutionExtensionFixture.Create();
        var reference = new TypeReferenceSignature(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service"));

        Assert.True(reference.TryResolve(fixture.Set, out var resolved));
        Assert.True(reference.TryResolve(fixture.Set, out var withReason, out var reason));

        Assert.Same(fixture.Dependency.Reader, resolved.Reader);
        Assert.Equal("Service", resolved.Name);
        Assert.Equal(resolved, withReason);
        Assert.Equal(ResolutionFailureReason.None, reason);
    }

    [Fact]
    public void TypeReferenceSignatureTryResolve_MissingAssembly_PreservesFailureReason()
    {
        using var fixture = MissingResolutionFixture.Create();
        var reference = new TypeReferenceSignature(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "Base"));

        Assert.False(reference.TryResolve(fixture.Set, out var resolved));
        Assert.False(reference.TryResolve(fixture.Set, out var withReason, out var reason));

        Assert.Equal(default, resolved);
        Assert.Equal(default, withReason);
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
        Assert.Null(reference.ResolveIsValueType(fixture.Set));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeReferenceSignatureTryResolve_NullSignature_ThrowsArgumentNull(bool includeReason)
    {
        using var set = new AssemblySet();
        TypeReferenceSignature reference = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            if (includeReason)
            {
                reference.TryResolve(set, out _, out _);
            }
            else
            {
                reference.TryResolve(set, out _);
            }
        });

        Assert.Equal("typeReference", exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeReferenceSignatureTryResolve_NullAssemblySet_ThrowsArgumentNull(bool includeReason)
    {
        using var fixture = ResolutionExtensionFixture.Create();
        var reference = new TypeReferenceSignature(
            fixture.Consumer.Reader,
            ResolutionTestHelpers.FindTypeReferenceHandle(fixture.Consumer.Reader, "Contracts", "Service"));

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            if (includeReason)
            {
                reference.TryResolve(null!, out _, out _);
            }
            else
            {
                reference.TryResolve(null!, out _);
            }
        });

        Assert.Equal("assemblySet", exception.ParamName);
    }

    [Fact]
    public void ResolutionDiagnostics_ReturnsMessagesForAllReasons()
    {
        foreach (var reason in Enum.GetValues<ResolutionFailureReason>())
        {
            string message = ResolutionDiagnostics.GetActionableMessage(reason);
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    [Fact]
    public void ResolutionDiagnostics_MessagesStayActionable()
    {
        Assert.Contains("AssemblySet.Add", ResolutionDiagnostics.GetActionableMessage(ResolutionFailureReason.AssemblyNotFound));
        Assert.Contains("MemberResolutionIndex", ResolutionDiagnostics.GetActionableMessage(ResolutionFailureReason.MemberAmbiguous));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EnumerateBaseTypes_CyclicMetadata_TerminatesWithoutReturningStartingType(int shape)
    {
        var nodes = shape switch
        {
            0 => new[] { ("A", 0, false, Array.Empty<int>()) },
            1 => new[] { ("A", 1, false, Array.Empty<int>()), ("B", 0, false, Array.Empty<int>()) },
            _ => new[]
            {
                ("A", 1, false, Array.Empty<int>()),
                ("B", 2, false, Array.Empty<int>()),
                ("C", 3, false, Array.Empty<int>()),
                ("D", 1, false, Array.Empty<int>())
            }
        };
        using var provider = CreateHierarchyProvider("CyclicBases", nodes);
        using var set = new AssemblySet();
        var type = TypeContext.Create(provider.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(2));

        var localBases = CollectLocalBases(type);
        var resolvedBases = CollectResolvedBases(type, set);

        string[] expectedNames = shape switch
        {
            0 => Array.Empty<string>(),
            1 => new[] { "B" },
            _ => new[] { "B", "C", "D" }
        };
        foreach (var bases in new[] { localBases, resolvedBases })
        {
            Assert.DoesNotContain(type, bases);
            Assert.Equal(expectedNames, bases.Select(candidate => candidate.Name).Distinct().OrderBy(name => name));
        }
    }

    [Fact]
    public void EnumerateBaseTypes_LongLocalChain_StaysCompleteAndAllocationFree()
    {
        var nodes = new (string Name, int BaseType, bool IsInterface, int[] Interfaces)[64];
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = ("Node" + i, i == nodes.Length - 1 ? -1 : i + 1, false, Array.Empty<int>());
        }

        using var provider = CreateHierarchyProvider("LongLocalChain", nodes);
        var type = TypeContext.Create(provider.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(2));
        int count = 0;
        for (int i = 0; i < 32; i++)
        {
            count += CountLocalBases(type);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            count += CountLocalBases(type);
        }
        long allocations = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(63 * 132, count);
        Assert.Equal(0L, allocations);
    }

    [Fact]
    public void EnumerateBaseTypes_SameHandleInDifferentReaders_ResolvesDistinctBase()
    {
        using var dependency = CreateHierarchyProvider("Dependency", ("Node", -1, false, Array.Empty<int>()));
        using var consumer = CreateReferenceHierarchyProvider(asInterfaces: false, "Dependency");
        using var set = new AssemblySet();
        var dependencyReader = dependency.GetMetadataReader();
        set.Add(dependencyReader);
        var type = TypeContext.Create(consumer.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(2));

        var baseType = Assert.Single(CollectResolvedBases(type, set));

        Assert.Equal(type.Handle, baseType.Handle);
        Assert.Same(dependencyReader, baseType.Reader);
    }

    [Fact]
    public void EnumerateAllInterfaces_DiamondAndBaseOverlap_ReturnsEachTransitiveInterfaceOnce()
    {
        using var provider = CreateHierarchyProvider(
            "InterfaceDiamond",
            ("IRoot", -1, true, Array.Empty<int>()),
            ("ILeft", -1, true, new[] { 0 }),
            ("IRight", -1, true, new[] { 0 }),
            ("IDiamond", -1, true, new[] { 1, 2 }),
            ("Base", -1, false, new[] { 0, 3 }),
            ("Derived", 4, false, new[] { 3 }));
        using var set = new AssemblySet();
        var type = TypeContext.Create(provider.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(7));

        var interfaces = CollectInterfaces(type, set);

        Assert.Equal(4, interfaces.Count);
        Assert.Equal(new[] { "IDiamond", "ILeft", "IRight", "IRoot" }, interfaces.Select(candidate => candidate.Name).OrderBy(name => name));
    }

    [Fact]
    public void EnumerateAllInterfaces_CyclicInterfaceParents_TerminatesAndExcludesStartingInterface()
    {
        using var provider = CreateHierarchyProvider(
            "InterfaceCycle",
            ("IChild", -1, true, new[] { 1 }),
            ("IParent", -1, true, new[] { 0 }),
            ("Consumer", -1, false, new[] { 0 }));
        using var set = new AssemblySet();
        var reader = provider.GetMetadataReader();
        var consumer = TypeContext.Create(reader, MetadataTokens.TypeDefinitionHandle(4));
        var child = TypeContext.Create(reader, MetadataTokens.TypeDefinitionHandle(2));

        var consumerInterfaces = CollectInterfaces(consumer, set);
        var inheritedInterfaces = CollectInterfaces(child, set);

        Assert.Equal(new[] { "IChild", "IParent" }, consumerInterfaces.Select(candidate => candidate.Name));
        Assert.Equal("IParent", Assert.Single(inheritedInterfaces).Name);
    }

    [Fact]
    public void EnumerateAllInterfaces_CyclicBaseTypes_TerminatesWithoutRepeatingInterfaces()
    {
        using var provider = CreateHierarchyProvider(
            "CyclicClassInterfaces",
            ("IRoot", -1, true, Array.Empty<int>()),
            ("A", 2, false, new[] { 0 }),
            ("B", 1, false, new[] { 0 }));
        using var set = new AssemblySet();
        var type = TypeContext.Create(provider.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(3));

        Assert.Equal("IRoot", Assert.Single(CollectInterfaces(type, set)).Name);
    }

    [Fact]
    public void EnumerateAllInterfaces_SameNameAndHandleInDifferentReaders_PreservesDistinctInterfaces()
    {
        using var first = CreateHierarchyProvider("First", ("Node", -1, true, Array.Empty<int>()));
        using var second = CreateHierarchyProvider("Second", ("Node", -1, true, Array.Empty<int>()));
        using var consumer = CreateReferenceHierarchyProvider(asInterfaces: true, "First", "Second");
        using var set = new AssemblySet();
        var firstReader = first.GetMetadataReader();
        var secondReader = second.GetMetadataReader();
        set.Add(firstReader);
        set.Add(secondReader);
        var type = TypeContext.Create(consumer.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(2));

        var interfaces = CollectInterfaces(type, set);

        Assert.Equal(2, interfaces.Count);
        Assert.Equal(interfaces[0].Handle, interfaces[1].Handle);
        Assert.Contains(interfaces, candidate => ReferenceEquals(candidate.Reader, firstReader));
        Assert.Contains(interfaces, candidate => ReferenceEquals(candidate.Reader, secondReader));
    }

    [Fact]
    public void EnumerateAllInterfaces_ReusedEnumerable_StartsIndependentWalks()
    {
        using var provider = CreateHierarchyProvider(
            "RepeatedInterfaceWalk",
            ("IRoot", -1, true, Array.Empty<int>()),
            ("Consumer", -1, false, new[] { 0 }));
        using var set = new AssemblySet();
        var type = TypeContext.Create(provider.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(3));
        var enumerable = type.EnumerateAllInterfaces(set);
        int count = 0;

        foreach (var unused in enumerable)
        {
            count++;
        }
        foreach (var unused in enumerable)
        {
            count++;
        }

        Assert.Equal(2, count);
    }

    private static int CountLocalBases(TypeContext type)
    {
        int count = 0;
        foreach (var unused in type.EnumerateBaseTypes())
        {
            count++;
        }
        return count;
    }

    private static List<TypeContext> CollectLocalBases(TypeContext type)
    {
        var results = new List<TypeContext>();
        var enumerator = type.EnumerateBaseTypes();
        for (int i = 0; i < 32; i++)
        {
            if (!enumerator.MoveNext())
            {
                return results;
            }
            results.Add(enumerator.Current);
        }
        throw new InvalidOperationException("Local base traversal did not terminate.");
    }

    private static List<TypeContext> CollectResolvedBases(TypeContext type, AssemblySet set)
    {
        var results = new List<TypeContext>();
        var enumerator = type.EnumerateBaseTypes(set);
        for (int i = 0; i < 32; i++)
        {
            if (!enumerator.MoveNext())
            {
                return results;
            }
            results.Add(enumerator.Current);
        }
        throw new InvalidOperationException("Resolved base traversal did not terminate.");
    }

    private static List<TypeContext> CollectInterfaces(TypeContext type, AssemblySet set)
    {
        var results = new List<TypeContext>();
        var enumerator = type.EnumerateAllInterfaces(set);
        for (int i = 0; i < 32; i++)
        {
            if (!enumerator.MoveNext())
            {
                return results;
            }
            results.Add(enumerator.Current);
        }
        throw new InvalidOperationException("Interface traversal did not terminate.");
    }

    private static MetadataReaderProvider CreateHierarchyProvider(
        string assemblyName,
        params (string Name, int BaseType, bool IsInterface, int[] Interfaces)[] types)
    {
        var metadata = CreateHierarchyMetadataBuilder(assemblyName);
        for (int index = 0; index < types.Length; index++)
        {
            var type = types[index];
            var attributes = TypeAttributes.Public;
            if (type.IsInterface)
            {
                attributes |= TypeAttributes.Interface | TypeAttributes.Abstract;
            }
            EntityHandle baseType = type.BaseType < 0 ? default : MetadataTokens.TypeDefinitionHandle(type.BaseType + 2);
            metadata.AddTypeDefinition(
                attributes, default, metadata.GetOrAddString(type.Name), baseType,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        }
        for (int index = 0; index < types.Length; index++)
        {
            foreach (int inherited in types[index].Interfaces)
            {
                metadata.AddInterfaceImplementation(
                    MetadataTokens.TypeDefinitionHandle(index + 2), MetadataTokens.TypeDefinitionHandle(inherited + 2));
            }
        }
        return CreateHierarchyMetadataProvider(metadata);
    }

    private static MetadataReaderProvider CreateReferenceHierarchyProvider(bool asInterfaces, params string[] assemblyNames)
    {
        var metadata = CreateHierarchyMetadataBuilder("HierarchyConsumer");
        var references = new TypeReferenceHandle[assemblyNames.Length];
        for (int index = 0; index < assemblyNames.Length; index++)
        {
            var assembly = metadata.AddAssemblyReference(
                metadata.GetOrAddString(assemblyNames[index]), new Version(1, 0, 0, 0),
                default, default, 0, default);
            references[index] = metadata.AddTypeReference(assembly, default, metadata.GetOrAddString("Node"));
        }
        var type = metadata.AddTypeDefinition(
            TypeAttributes.Public, default, metadata.GetOrAddString("Node"), asInterfaces ? default : references[0],
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        if (asInterfaces)
        {
            foreach (var reference in references)
            {
                metadata.AddInterfaceImplementation(type, reference);
            }
        }
        return CreateHierarchyMetadataProvider(metadata);
    }

    private static MetadataBuilder CreateHierarchyMetadataBuilder(string assemblyName)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(assemblyName + ".dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(assemblyName), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(0, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static MetadataReaderProvider CreateHierarchyMetadataProvider(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }

    private sealed class ResolutionExtensionFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace;

        private ResolutionExtensionFixture(
            TemporaryDirectory workspace,
            MetadataScope dependency,
            MetadataScope consumer,
            AssemblySet set,
            TypeContext derived,
            TypeContext genericDerived)
        {
            _workspace = workspace;
            Dependency = dependency;
            Consumer = consumer;
            Set = set;
            Derived = derived;
            GenericDerived = genericDerived;
        }

        public MetadataScope Dependency { get; }

        public MetadataScope Consumer { get; }

        public AssemblySet Set { get; }

        public TypeContext Derived { get; }

        public TypeContext GenericDerived { get; }

        public static ResolutionExtensionFixture Create()
        {
            var workspace = new TemporaryDirectory();

            string referenceDir = workspace.CreateDirectory("refs");
            string dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
                referenceDir,
                "ContractsLibrary",
                """
                namespace Contracts;

                public interface IRoot
                {
                }

                public interface IGeneric<T>
                {
                }

                public class Base : IRoot
                {
                }

                public class Middle : Base
                {
                }

                public class GenericBase<T> : IGeneric<T>
                {
                }

                public struct ExternalStruct
                {
                    public int Value;
                }

                public sealed class Service
                {
                    public int Value;

                    public int Echo(int value)
                    {
                        Value = value;
                        return value;
                    }
                }
                """);

            var dependency = new MetadataScope(dependencyPath);
            var consumer = new TestAssemblyBuilder("ConsumerAssembly")
                .WithSource(
                    """
                    using Contracts;

                    namespace ConsumerNamespace;

                    public sealed class Derived : Middle
                    {
                    }

                    public sealed class GenericDerived : GenericBase<int>
                    {
                    }

                    public sealed class Consumer
                    {
                        public int Run(Service service)
                        {
                            service.Value = service.Echo(42);
                            return service.Value;
                        }

                        public ExternalStruct ReturnStruct(ExternalStruct value)
                        {
                            return value;
                        }
                    }
                    """)
                .WithReference(dependencyPath)
                .BuildMetadataScope();

            var set = new AssemblySet();
            set.Add(dependency.Reader);

            var assembly = AssemblyContext.Create(consumer.Reader);
            Assert.True(assembly.TryFindType("ConsumerNamespace", "Derived", out var derived));
            Assert.True(assembly.TryFindType("ConsumerNamespace", "GenericDerived", out var genericDerived));

            return new ResolutionExtensionFixture(workspace, dependency, consumer, set, derived, genericDerived);
        }

        public void Dispose()
        {
            Set.Dispose();
            Consumer.Dispose();
            Dependency.Dispose();
            _workspace.Dispose();
        }
    }

    private sealed class MissingResolutionFixture : IDisposable
    {
        private readonly TemporaryDirectory _workspace;

        private MissingResolutionFixture(
            TemporaryDirectory workspace,
            MetadataScope consumer,
            AssemblySet set,
            TypeContext derived)
        {
            _workspace = workspace;
            Consumer = consumer;
            Set = set;
            Derived = derived;
        }

        public MetadataScope Consumer { get; }

        public AssemblySet Set { get; }

        public TypeContext Derived { get; }

        public static MissingResolutionFixture Create()
        {
            var workspace = new TemporaryDirectory();

            string referenceDir = workspace.CreateDirectory("refs");
            string dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
                referenceDir,
                "ContractsLibrary",
                """
                namespace Contracts;

                public class Base
                {
                }

                public sealed class Service
                {
                    public int Echo(int value)
                    {
                        return value;
                    }
                }
                """);

            var consumer = new TestAssemblyBuilder("ConsumerAssembly")
                .WithSource(
                    """
                    using Contracts;

                    namespace ConsumerNamespace;

                    public sealed class Derived : Base
                    {
                    }

                    public sealed class Consumer
                    {
                        public int Run(Service service)
                        {
                            return service.Echo(42);
                        }
                    }
                    """)
                .WithReference(dependencyPath)
                .BuildMetadataScope();

            var set = new AssemblySet();
            var assembly = AssemblyContext.Create(consumer.Reader);
            Assert.True(assembly.TryFindType("ConsumerNamespace", "Derived", out var derived));

            return new MissingResolutionFixture(workspace, consumer, set, derived);
        }

        public void Dispose()
        {
            Set.Dispose();
            Consumer.Dispose();
            _workspace.Dispose();
        }
    }
}

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Decoding;
using Glean.Providers;
using Glean.Resolution;
using Glean.Signatures;
using Glean.Tests.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Performance;

public sealed class AllocationBehaviorTests
{
    private const string LocalMetadataSource = """
        using System;

        namespace Glean.Tests.Performance;

        [Obsolete("legacy")]
        public sealed class PerfFixture
        {
            public int Value;

            public int Echo(int value)
            {
                Value = value;
                return value;
            }
        }
        """;
    private PrimitiveClassifierVisitor _primitiveClassifierVisitor;

    [Fact]
    public void TypeContextIs_RepeatedChecks_AllocatesNearRawSrm()
    {
        using var metadata = TestUtility.BuildMetadata(LocalMetadataSource);

        var typeHandle = metadata.FindTypeDefinitionHandle("Glean.Tests.Performance", "PerfFixture");
        var type = TypeContext.Create(metadata.Reader, typeHandle);
        var definition = type.Definition;
        var reader = metadata.Reader;

        long rawAllocations = MeasureAllocations(
            () =>
            {
                if (!reader.StringComparer.Equals(definition.Namespace, "Glean.Tests.Performance") ||
                    !reader.StringComparer.Equals(definition.Name, "PerfFixture"))
                {
                    throw new InvalidOperationException("Raw metadata comparison failed.");
                }
            },
            20_000);

        long wrapperAllocations = MeasureAllocations(
            () =>
            {
                if (!type.Is("Glean.Tests.Performance", "PerfFixture"))
                {
                    throw new InvalidOperationException("TypeContext.Is comparison failed.");
                }
            },
            20_000);

        Assert.InRange(wrapperAllocations, 0L, rawAllocations + 64L);
    }

    [Fact]
    public void AssemblyContextEnumerateTypes_RepeatedScans_AllocatesNearRawSrm()
    {
        using var metadata = TestUtility.BuildMetadata(LocalMetadataSource);

        var assembly = AssemblyContext.Create(metadata.Reader);
        int expectedRowSum = GetRawTypeRowSum(metadata.Reader);

        long rawAllocations = MeasureAllocations(
            () =>
            {
                int rowSum = GetRawTypeRowSum(metadata.Reader);
                if (rowSum != expectedRowSum)
                {
                    throw new InvalidOperationException("Raw type scan failed.");
                }
            },
            5_000);

        long wrapperAllocations = MeasureAllocations(
            () =>
            {
                int rowSum = GetWrappedTypeRowSum(assembly);
                if (rowSum != expectedRowSum)
                {
                    throw new InvalidOperationException("AssemblyContext type scan failed.");
                }
            },
            5_000);

        Assert.InRange(wrapperAllocations, 0L, rawAllocations + 128L);
    }

    [Fact]
    public void TypeContextFullName_RepeatedReads_Allocates()
    {
        using var metadata = TestUtility.BuildMetadata(LocalMetadataSource);

        var typeHandle = metadata.FindTypeDefinitionHandle("Glean.Tests.Performance", "PerfFixture");
        var type = TypeContext.Create(metadata.Reader, typeHandle);
        int totalLength = 0;

        long allocations = MeasureAllocations(
            () =>
            {
                totalLength += type.FullName.Length;
            },
            2_000);

        GC.KeepAlive(totalLength);
        Assert.True(allocations > 0);
    }

    [Fact]
    public void CustomAttributeDecoderDecode_RepeatedDecodes_Allocates()
    {
        using var metadata = TestUtility.BuildMetadata(LocalMetadataSource);

        var typeHandle = metadata.FindTypeDefinitionHandle("Glean.Tests.Performance", "PerfFixture");
        var type = metadata.Reader.GetTypeDefinition(typeHandle);
        var attributeHandle = GetSingleAttributeHandle(type);

        long allocations = MeasureAllocations(
            () =>
            {
                var decoded = CustomAttributeDecoder.Decode(metadata.Reader, attributeHandle);
                if (decoded.FixedArguments.Length != 1)
                {
                    throw new InvalidOperationException("Custom attribute decode failed.");
                }
            },
            2_000);

        Assert.True(allocations > 0);
    }

    [Fact]
    public void TypeSignatureAccept_WithStructVisitor_RepeatedDispatchStaysAllocationFree()
    {
        var signature = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        _primitiveClassifierVisitor = default;

        signature.Accept(ref _primitiveClassifierVisitor, state: 0);
        Assert.Equal(PrimitiveTypeCode.Int32, _primitiveClassifierVisitor.LastTypeCode);

        long allocations = MeasureAllocations(
            () =>
            {
                signature.Accept(ref _primitiveClassifierVisitor, state: 0);
                if (_primitiveClassifierVisitor.LastTypeCode != PrimitiveTypeCode.Int32)
                {
                    throw new InvalidOperationException("Visitor dispatch returned an unexpected type code.");
                }
            },
            20_000);

        Assert.InRange(allocations, 0L, 64L);
        Assert.True(_primitiveClassifierVisitor.VisitCount > 0);
    }

    [Fact]
    public void AssemblySetTryResolveType_WarmCache_RepeatedLookupsStayAllocationFree()
    {
        using var workspace = new TemporaryDirectory();

        var referenceDir = workspace.CreateDirectory("refs");
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
            referenceDir,
            "DependencyLibrary",
            """
            namespace Fixture;

            public sealed class Dependency
            {
            }
            """);

        using var dependency = new MetadataScope(dependencyPath);
        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    private readonly Dependency _dependency = new();
                }
                """)
            .WithReference(dependencyPath)
            .BuildMetadataScope();

        using var set = new AssemblySet();
        set.Add(dependency.Reader);

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(
            consumer.Reader,
            "Fixture",
            "Dependency");

        Assert.True(set.TryResolveType(consumer.Reader, typeReferenceHandle, out _, out _));

        long allocations = MeasureAllocations(
            () =>
            {
                if (!set.TryResolveType(consumer.Reader, typeReferenceHandle, out var targetReader, out var targetHandle))
                {
                    throw new InvalidOperationException("Type resolution failed.");
                }

                if (!ReferenceEquals(targetReader, dependency.Reader) || targetHandle.IsNil)
                {
                    throw new InvalidOperationException("Resolved type target was unexpected.");
                }
            },
            20_000);

        Assert.InRange(allocations, 0L, 128L);
    }

    [Fact]
    public void AssemblySetTryResolveMember_WithWarmedIndex_RepeatedLookupsStayAllocationFree()
    {
        using var workspace = new TemporaryDirectory();

        var referenceDir = workspace.CreateDirectory("refs");
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
            referenceDir,
            "DependencyLibrary",
            """
            namespace Fixture;

            public sealed class Dependency
            {
                public int Value;

                public int Echo(int value)
                {
                    Value = value;
                    return value;
                }
            }
            """);

        using var dependency = new MetadataScope(dependencyPath);
        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    public int Run(Dependency dependency)
                    {
                        dependency.Value = dependency.Echo(42);
                        return dependency.Value;
                    }
                }
                """)
            .WithReference(dependencyPath)
            .BuildMetadataScope();

        using var set = new AssemblySet();
        set.Add(dependency.Reader);

        var memberReferenceHandle = ResolutionTestHelpers.FindMemberReferenceHandle(
            consumer.Reader,
            "Fixture",
            "Dependency",
            "Echo");
        var index = new MemberResolutionIndex(cacheSemanticSignatures: false);

        Assert.True(set.TryResolveMember(consumer.Reader, memberReferenceHandle, index, out _, out _));

        long allocations = MeasureAllocations(
            () =>
            {
                if (!set.TryResolveMember(consumer.Reader, memberReferenceHandle, index, out var targetReader, out var targetHandle))
                {
                    throw new InvalidOperationException("Member resolution failed.");
                }

                if (!ReferenceEquals(targetReader, dependency.Reader) || (targetHandle.Kind != HandleKind.MethodDefinition))
                {
                    throw new InvalidOperationException("Resolved member target was unexpected.");
                }
            },
            20_000);

        Assert.InRange(allocations, 0L, 256L);
    }

    private static CustomAttributeHandle GetSingleAttributeHandle(TypeDefinition type)
    {
        foreach (var handle in type.GetCustomAttributes())
        {
            return handle;
        }

        throw new InvalidOperationException("Expected a custom attribute.");
    }

    private static int GetRawTypeRowSum(MetadataReader reader)
    {
        int rowSum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            rowSum += MetadataTokens.GetRowNumber(handle);
        }

        return rowSum;
    }

    private static int GetWrappedTypeRowSum(AssemblyContext assembly)
    {
        int rowSum = 0;
        foreach (var type in assembly.EnumerateTypes())
        {
            rowSum += MetadataTokens.GetRowNumber(type.Handle);
        }

        return rowSum;
    }

    private static long MeasureAllocations(Action action, int iterations)
    {
        for (int i = 0; i < 32; i++)
        {
            action();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private struct PrimitiveClassifierVisitor : ITypeSignatureVisitor<int>
    {
        public int VisitCount;
        public PrimitiveTypeCode LastTypeCode;

        public void VisitArray(ArraySignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitByRef(ByRefSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitFunctionPointer(FunctionPointerSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitGenericInstance(GenericInstanceSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitGenericMethodParameter(GenericMethodParameterSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitGenericTypeParameter(GenericTypeParameterSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitModifiedType(ModifiedTypeSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitPinnedType(PinnedTypeSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitPointer(PointerSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitPrimitive(PrimitiveTypeSignature signature, int state)
        {
            VisitCount += state + 1;
            LastTypeCode = signature.TypeCode;
        }

        public void VisitSerializedTypeName(SerializedTypeNameSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitSentinel(SentinelTypeSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitSZArray(SZArraySignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitTypeDefinition(TypeDefinitionSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }

        public void VisitTypeReference(TypeReferenceSignature signature, int state)
        {
            throw new InvalidOperationException("Unexpected signature kind.");
        }
    }
}

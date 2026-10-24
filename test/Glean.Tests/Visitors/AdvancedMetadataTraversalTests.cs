using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Tests.Utility;
using Glean.Visitors;

namespace Glean.Tests.Visitors;

public sealed class AdvancedMetadataTraversalTests
{
    private const string ModuleSource = """
        public static class LinkedModuleType
        {
            public static int Read()
            {
                return 1;
            }
        }
        """;

    private const string AdvancedTraversalFixtureSource = """
        using System;
        using System.Collections.Generic;
        using System.Runtime.InteropServices;

        [assembly: Marker("assembly")]
        [module: Marker("module")]

        [AttributeUsage(AttributeTargets.All)]
        public sealed class MarkerAttribute : Attribute
        {
            public MarkerAttribute(string name)
            {
            }
        }

        public static class NativeMethods
        {
            [DllImport("kernel32.dll", EntryPoint = "GetTickCount")]
            public static extern uint GetTickCount();
        }

        [Marker("type")]
        public class AdvancedTraversalConsumer<T>
        {
            public const int ConstantValue = 42;

            public int ReadFromModule()
            {
                return LinkedModuleType.Read();
            }

            [Marker("method")]
            public List<int> Run<[Marker("method-generic")] U>([Marker("parameter")] int value = 7)
            {
                List<int> local = new();
                Generic<int>(value);
                return local;
            }

            public V Generic<V>(V value)
            {
                return value;
            }
        }
        """;

    [Fact]
    public void TraverseWithAssemblyReferences_VisitsAssemblyModuleAndFileTables()
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new AssemblyReferenceCountingVisitor();

        MetadataTraversal.TraverseWithAssemblyReferences(reader, ref visitor);

        Assert.Equal(reader.AssemblyReferences.Count, visitor.AssemblyReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ModuleRef), visitor.ModuleReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.File), visitor.AssemblyFileCount);
        Assert.Equal(reader.TypeDefinitions.Count, visitor.TypeCount);
        Assert.True(reader.GetTableRowCount(TableIndex.ModuleRef) > 0);
        Assert.True(reader.GetTableRowCount(TableIndex.File) > 0);
        Assert.Contains("kernel32.dll", visitor.ModuleReferenceNames);
    }

    [Fact]
    public void TraverseWithAssemblyReferences_StatefulVisitorMutatesCallerOwnedState()
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new StatefulAssemblyReferenceCountingVisitor();
        var state = new AdvancedTraversalState();

        MetadataTraversal.TraverseWithAssemblyReferences(reader, ref visitor, ref state);

        Assert.Equal(reader.AssemblyReferences.Count, state.AssemblyReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ModuleRef), state.ModuleReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.File), state.AssemblyFileCount);
        Assert.Equal(reader.TypeDefinitions.Count, state.TypeCount);
    }

    [Fact]
    public void TraverseWithConstantAndDebugTables_VisitsConstantSecurityAndDebugTables()
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new ConstantAndDebugCountingVisitor();

        MetadataTraversal.TraverseWithConstantAndDebugTables(reader, ref visitor);

        Assert.Equal(reader.GetTableRowCount(TableIndex.Constant), visitor.ConstantCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.DeclSecurity), visitor.DeclarativeSecurityCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.CustomDebugInformation), visitor.CustomDebugInformationCount);
        Assert.True(reader.GetTableRowCount(TableIndex.Constant) > 0);
    }

    [Fact]
    public void TraverseWithConstantAndDebugTables_StatefulVisitorMutatesCallerOwnedState()
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new StatefulConstantAndDebugCountingVisitor();
        var state = new AdvancedTraversalState();

        MetadataTraversal.TraverseWithConstantAndDebugTables(reader, ref visitor, ref state);

        Assert.Equal(reader.GetTableRowCount(TableIndex.Constant), state.ConstantCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.DeclSecurity), state.DeclarativeSecurityCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.CustomDebugInformation), state.CustomDebugInformationCount);
        Assert.Equal(reader.TypeDefinitions.Count, state.TypeCount);
    }

    [Fact]
    public void TraverseAllTables_VisitsAllAdvancedCoverage()
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new CompleteCountingVisitor();

        MetadataTraversal.TraverseAllTables(reader, ref visitor);

        Assert.Equal(reader.AssemblyReferences.Count, visitor.AssemblyReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ModuleRef), visitor.ModuleReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.File), visitor.AssemblyFileCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeSpec), visitor.TypeSpecificationCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.MethodSpec), visitor.MethodSpecificationCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.StandAloneSig), visitor.StandaloneSignatureCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.Constant), visitor.ConstantCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.DeclSecurity), visitor.DeclarativeSecurityCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.CustomDebugInformation), visitor.CustomDebugInformationCount);
        Assert.Equal(reader.TypeDefinitions.Count, visitor.TypeCount);
    }

    [Fact]
    public void TraverseAllTables_StatefulVisitorMutatesCallerOwnedState()
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new StatefulCompleteCountingVisitor();
        var state = new AdvancedTraversalState();

        MetadataTraversal.TraverseAllTables(reader, ref visitor, ref state);

        Assert.Equal(reader.AssemblyReferences.Count, state.AssemblyReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.ModuleRef), state.ModuleReferenceCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.File), state.AssemblyFileCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.TypeSpec), state.TypeSpecificationCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.MethodSpec), state.MethodSpecificationCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.StandAloneSig), state.StandaloneSignatureCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.Constant), state.ConstantCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.DeclSecurity), state.DeclarativeSecurityCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.CustomDebugInformation), state.CustomDebugInformationCount);
        Assert.Equal(reader.TypeDefinitions.Count, state.TypeCount);
    }

    [Theory]
    [InlineData(TraversalProfile.Standard)]
    [InlineData(TraversalProfile.MethodsOnly)]
    [InlineData(TraversalProfile.MethodAttributesOnly)]
    [InlineData(TraversalProfile.AssemblyReferences)]
    [InlineData(TraversalProfile.SignatureTables)]
    [InlineData(TraversalProfile.ConstantAndDebugTables)]
    [InlineData(TraversalProfile.AllTables)]
    public void Traverse_WithStructVisitor_WarmedScansDoNotAllocate(TraversalProfile profile)
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new CompleteCountingVisitor();

        for (int i = 0; i < 32; i++)
        {
            TraverseProfile(profile, reader, ref visitor);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            TraverseProfile(profile, reader, ref visitor);
        }
        long allocations = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocations);
        Assert.Equal(reader.TypeDefinitions.Count * 132, visitor.TypeCount);
    }

    [Theory]
    [InlineData(TraversalProfile.Standard)]
    [InlineData(TraversalProfile.MethodsOnly)]
    [InlineData(TraversalProfile.MethodAttributesOnly)]
    [InlineData(TraversalProfile.AssemblyReferences)]
    [InlineData(TraversalProfile.SignatureTables)]
    [InlineData(TraversalProfile.ConstantAndDebugTables)]
    [InlineData(TraversalProfile.AllTables)]
    public void Traverse_WithStructVisitorAndValueState_WarmedScansDoNotAllocate(TraversalProfile profile)
    {
        using var scope = new AdvancedTraversalScope();
        var reader = scope.Metadata.Reader;
        var visitor = new StatefulCompleteCountingVisitor();
        var state = new AdvancedTraversalState();

        for (int i = 0; i < 32; i++)
        {
            TraverseStatefulProfile(profile, reader, ref visitor, ref state);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            TraverseStatefulProfile(profile, reader, ref visitor, ref state);
        }
        long allocations = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocations);
        Assert.Equal(reader.TypeDefinitions.Count * 132, state.TypeCount);
        Assert.Equal(state.TypeCount, visitor.TypeCount);
    }

    [Theory]
    [InlineData(TraversalProfile.Standard)]
    [InlineData(TraversalProfile.MethodsOnly)]
    [InlineData(TraversalProfile.MethodAttributesOnly)]
    [InlineData(TraversalProfile.AssemblyReferences)]
    [InlineData(TraversalProfile.SignatureTables)]
    [InlineData(TraversalProfile.ConstantAndDebugTables)]
    [InlineData(TraversalProfile.AllTables)]
    public void Traverse_StatefulCallbackThrows_PreservesVisitorAndValueState(TraversalProfile profile)
    {
        using var scope = new AdvancedTraversalScope();
        var visitor = new StatefulCompleteCountingVisitor { ThrowOnType = true };
        var state = new AdvancedTraversalState();

        var exception = Assert.Throws<InvalidOperationException>(
            () => TraverseStatefulProfile(profile, scope.Metadata.Reader, ref visitor, ref state));

        Assert.Equal("Traversal callback failed.", exception.Message);
        Assert.Equal(1, visitor.TypeCount);
        Assert.Equal(1, state.TypeCount);
    }

    [Theory]
    [InlineData(TraversalProfile.Standard)]
    [InlineData(TraversalProfile.MethodsOnly)]
    [InlineData(TraversalProfile.MethodAttributesOnly)]
    [InlineData(TraversalProfile.AssemblyReferences)]
    [InlineData(TraversalProfile.SignatureTables)]
    [InlineData(TraversalProfile.ConstantAndDebugTables)]
    [InlineData(TraversalProfile.AllTables)]
    public void Traverse_StatefulCallbackCancels_PreservesVisitorAndValueState(TraversalProfile profile)
    {
        using var scope = new AdvancedTraversalScope();
        using var cancellationSource = new CancellationTokenSource();
        var visitor = new StatefulCompleteCountingVisitor { CancellationSource = cancellationSource };
        var state = new AdvancedTraversalState();

        var exception = Assert.Throws<OperationCanceledException>(
            () => TraverseStatefulProfile(profile, scope.Metadata.Reader, ref visitor, ref state, cancellationSource.Token));

        Assert.Equal(cancellationSource.Token, exception.CancellationToken);
        Assert.Equal(1, visitor.TypeCount);
        Assert.Equal(1, state.TypeCount);
    }

    public enum TraversalProfile
    {
        Standard,
        MethodsOnly,
        MethodAttributesOnly,
        AssemblyReferences,
        SignatureTables,
        ConstantAndDebugTables,
        AllTables
    }

    private static void TraverseProfile(TraversalProfile profile, MetadataReader reader, ref CompleteCountingVisitor visitor)
    {
        switch (profile)
        {
            case TraversalProfile.Standard:
                MetadataTraversal.Traverse(reader, ref visitor);
                break;
            case TraversalProfile.MethodsOnly:
                MetadataTraversal.TraverseMethodsOnly(reader, ref visitor);
                break;
            case TraversalProfile.MethodAttributesOnly:
                MetadataTraversal.TraverseMethodAttributesOnly(reader, ref visitor);
                break;
            case TraversalProfile.AssemblyReferences:
                MetadataTraversal.TraverseWithAssemblyReferences(reader, ref visitor);
                break;
            case TraversalProfile.SignatureTables:
                MetadataTraversal.TraverseWithSignatureTables(reader, ref visitor);
                break;
            case TraversalProfile.ConstantAndDebugTables:
                MetadataTraversal.TraverseWithConstantAndDebugTables(reader, ref visitor);
                break;
            case TraversalProfile.AllTables:
                MetadataTraversal.TraverseAllTables(reader, ref visitor);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile));
        }
    }

    private static void TraverseStatefulProfile(
        TraversalProfile profile,
        MetadataReader reader,
        ref StatefulCompleteCountingVisitor visitor,
        ref AdvancedTraversalState state,
        CancellationToken cancellationToken = default)
    {
        switch (profile)
        {
            case TraversalProfile.Standard:
                MetadataTraversal.Traverse(reader, ref visitor, ref state, cancellationToken);
                break;
            case TraversalProfile.MethodsOnly:
                MetadataTraversal.TraverseMethodsOnly(reader, ref visitor, ref state, cancellationToken);
                break;
            case TraversalProfile.MethodAttributesOnly:
                MetadataTraversal.TraverseMethodAttributesOnly(reader, ref visitor, ref state, cancellationToken);
                break;
            case TraversalProfile.AssemblyReferences:
                MetadataTraversal.TraverseWithAssemblyReferences(reader, ref visitor, ref state, cancellationToken);
                break;
            case TraversalProfile.SignatureTables:
                MetadataTraversal.TraverseWithSignatureTables(reader, ref visitor, ref state, cancellationToken);
                break;
            case TraversalProfile.ConstantAndDebugTables:
                MetadataTraversal.TraverseWithConstantAndDebugTables(reader, ref visitor, ref state, cancellationToken);
                break;
            case TraversalProfile.AllTables:
                MetadataTraversal.TraverseAllTables(reader, ref visitor, ref state, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile));
        }
    }

    private sealed class AdvancedTraversalScope : IDisposable
    {
        private readonly TemporaryDirectory _directory;

        public AdvancedTraversalScope()
        {
            _directory = new TemporaryDirectory();

            string modulePath = TestAssemblyFiles.BuildModuleFile(
                _directory.Path,
                "LinkedModule",
                ModuleSource);

            Metadata = new TestAssemblyBuilder("AdvancedTraversalAssembly")
                .WithSource(AdvancedTraversalFixtureSource)
                .WithModuleReference(modulePath)
                .BuildMetadataScope();
        }

        public MetadataScope Metadata { get; }

        public void Dispose()
        {
            Metadata.Dispose();
            _directory.Dispose();
        }
    }

    private struct AssemblyReferenceCountingVisitor : IMetadataVisitor, IAssemblyReferenceVisitor
    {
        public int TypeCount;
        public int AssemblyReferenceCount;
        public int ModuleReferenceCount;
        public int AssemblyFileCount;
        public List<string> ModuleReferenceNames;

        public bool VisitType(in TypeContext context)
        {
            TypeCount++;
            return true;
        }

        public void VisitAssemblyReference(in AssemblyReferenceContext context)
        {
            AssemblyReferenceCount++;
        }

        public void VisitModuleReference(in ModuleReferenceContext context)
        {
            ModuleReferenceCount++;
            ModuleReferenceNames ??= new List<string>();
            ModuleReferenceNames.Add(context.Name);
        }

        public void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle)
        {
            AssemblyFileCount++;
        }

        public void VisitMethod(in MethodContext context)
        {
        }

        public void VisitField(in FieldContext context)
        {
        }

        public void VisitProperty(in PropertyContext context)
        {
        }

        public void VisitEvent(in EventContext context)
        {
        }

        public void VisitParameter(in ParameterContext context)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
        }
    }

    private struct ConstantAndDebugCountingVisitor : IMetadataVisitor, IConstantAndDebugMetadataVisitor
    {
        public int ConstantCount;
        public int DeclarativeSecurityCount;
        public int CustomDebugInformationCount;

        public bool VisitType(in TypeContext context)
        {
            return true;
        }

        public void VisitConstant(in Constant constant, ConstantHandle handle)
        {
            ConstantCount++;
        }

        public void VisitDeclarativeSecurityAttribute(
            in DeclarativeSecurityAttribute attribute,
            DeclarativeSecurityAttributeHandle handle)
        {
            DeclarativeSecurityCount++;
        }

        public void VisitCustomDebugInformation(in CustomDebugInformation info, CustomDebugInformationHandle handle)
        {
            CustomDebugInformationCount++;
        }

        public void VisitMethod(in MethodContext context)
        {
        }

        public void VisitField(in FieldContext context)
        {
        }

        public void VisitProperty(in PropertyContext context)
        {
        }

        public void VisitEvent(in EventContext context)
        {
        }

        public void VisitParameter(in ParameterContext context)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
        }
    }

    private struct CompleteCountingVisitor : ICompleteMetadataVisitor
    {
        public int TypeCount;
        public int AssemblyReferenceCount;
        public int ModuleReferenceCount;
        public int AssemblyFileCount;
        public int TypeSpecificationCount;
        public int MethodSpecificationCount;
        public int StandaloneSignatureCount;
        public int ConstantCount;
        public int DeclarativeSecurityCount;
        public int CustomDebugInformationCount;

        public bool VisitType(in TypeContext context)
        {
            TypeCount++;
            return true;
        }

        public void VisitAssemblyReference(in AssemblyReferenceContext context)
        {
            AssemblyReferenceCount++;
        }

        public void VisitModuleReference(in ModuleReferenceContext context)
        {
            ModuleReferenceCount++;
        }

        public void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle)
        {
            AssemblyFileCount++;
        }

        public void VisitTypeSpecification(in TypeSpecification typeSpecification, TypeSpecificationHandle handle)
        {
            TypeSpecificationCount++;
        }

        public void VisitMethodSpecification(in MethodSpecification methodSpecification, MethodSpecificationHandle handle)
        {
            MethodSpecificationCount++;
        }

        public void VisitStandaloneSignature(in StandaloneSignature signature, StandaloneSignatureHandle handle)
        {
            StandaloneSignatureCount++;
        }

        public void VisitConstant(in Constant constant, ConstantHandle handle)
        {
            ConstantCount++;
        }

        public void VisitDeclarativeSecurityAttribute(
            in DeclarativeSecurityAttribute attribute,
            DeclarativeSecurityAttributeHandle handle)
        {
            DeclarativeSecurityCount++;
        }

        public void VisitCustomDebugInformation(in CustomDebugInformation info, CustomDebugInformationHandle handle)
        {
            CustomDebugInformationCount++;
        }

        public void VisitMethod(in MethodContext context)
        {
        }

        public void VisitField(in FieldContext context)
        {
        }

        public void VisitProperty(in PropertyContext context)
        {
        }

        public void VisitEvent(in EventContext context)
        {
        }

        public void VisitParameter(in ParameterContext context)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
        }
    }

    private struct StatefulAssemblyReferenceCountingVisitor :
        IStatefulMetadataVisitor<AdvancedTraversalState>,
        IStatefulAssemblyReferenceVisitor<AdvancedTraversalState>
    {
        public bool VisitType(in TypeContext context, ref AdvancedTraversalState state)
        {
            state.TypeCount++;
            return true;
        }

        public void VisitAssemblyReference(in AssemblyReferenceContext context, ref AdvancedTraversalState state)
        {
            state.AssemblyReferenceCount++;
        }

        public void VisitModuleReference(in ModuleReferenceContext context, ref AdvancedTraversalState state)
        {
            state.ModuleReferenceCount++;
        }

        public void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle, ref AdvancedTraversalState state)
        {
            state.AssemblyFileCount++;
        }

        public void VisitMethod(in MethodContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitField(in FieldContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitParameter(in ParameterContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref AdvancedTraversalState state)
        {
        }
    }

    private struct StatefulConstantAndDebugCountingVisitor :
        IStatefulMetadataVisitor<AdvancedTraversalState>,
        IStatefulConstantAndDebugMetadataVisitor<AdvancedTraversalState>
    {
        public bool VisitType(in TypeContext context, ref AdvancedTraversalState state)
        {
            state.TypeCount++;
            return true;
        }

        public void VisitConstant(in Constant constant, ConstantHandle handle, ref AdvancedTraversalState state)
        {
            state.ConstantCount++;
        }

        public void VisitDeclarativeSecurityAttribute(
            in DeclarativeSecurityAttribute attribute,
            DeclarativeSecurityAttributeHandle handle,
            ref AdvancedTraversalState state)
        {
            state.DeclarativeSecurityCount++;
        }

        public void VisitCustomDebugInformation(
            in CustomDebugInformation info,
            CustomDebugInformationHandle handle,
            ref AdvancedTraversalState state)
        {
            state.CustomDebugInformationCount++;
        }

        public void VisitMethod(in MethodContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitField(in FieldContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitParameter(in ParameterContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref AdvancedTraversalState state)
        {
        }
    }

    private struct StatefulCompleteCountingVisitor : IStatefulCompleteMetadataVisitor<AdvancedTraversalState>
    {
        public int TypeCount;
        public bool ThrowOnType;
        public CancellationTokenSource? CancellationSource;

        public bool VisitType(in TypeContext context, ref AdvancedTraversalState state)
        {
            TypeCount++;
            state.TypeCount++;
            if (ThrowOnType)
            {
                throw new InvalidOperationException("Traversal callback failed.");
            }

            CancellationSource?.Cancel();
            return true;
        }

        public void VisitAssemblyReference(in AssemblyReferenceContext context, ref AdvancedTraversalState state)
        {
            state.AssemblyReferenceCount++;
        }

        public void VisitModuleReference(in ModuleReferenceContext context, ref AdvancedTraversalState state)
        {
            state.ModuleReferenceCount++;
        }

        public void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle, ref AdvancedTraversalState state)
        {
            state.AssemblyFileCount++;
        }

        public void VisitTypeSpecification(
            in TypeSpecification typeSpecification,
            TypeSpecificationHandle handle,
            ref AdvancedTraversalState state)
        {
            state.TypeSpecificationCount++;
        }

        public void VisitMethodSpecification(
            in MethodSpecification methodSpecification,
            MethodSpecificationHandle handle,
            ref AdvancedTraversalState state)
        {
            state.MethodSpecificationCount++;
        }

        public void VisitStandaloneSignature(
            in StandaloneSignature signature,
            StandaloneSignatureHandle handle,
            ref AdvancedTraversalState state)
        {
            state.StandaloneSignatureCount++;
        }

        public void VisitConstant(in Constant constant, ConstantHandle handle, ref AdvancedTraversalState state)
        {
            state.ConstantCount++;
        }

        public void VisitDeclarativeSecurityAttribute(
            in DeclarativeSecurityAttribute attribute,
            DeclarativeSecurityAttributeHandle handle,
            ref AdvancedTraversalState state)
        {
            state.DeclarativeSecurityCount++;
        }

        public void VisitCustomDebugInformation(
            in CustomDebugInformation info,
            CustomDebugInformationHandle handle,
            ref AdvancedTraversalState state)
        {
            state.CustomDebugInformationCount++;
        }

        public void VisitMethod(in MethodContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitField(in FieldContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitParameter(in ParameterContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref AdvancedTraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref AdvancedTraversalState state)
        {
        }
    }

    private struct AdvancedTraversalState
    {
        public int TypeCount;
        public int AssemblyReferenceCount;
        public int ModuleReferenceCount;
        public int AssemblyFileCount;
        public int TypeSpecificationCount;
        public int MethodSpecificationCount;
        public int StandaloneSignatureCount;
        public int ConstantCount;
        public int DeclarativeSecurityCount;
        public int CustomDebugInformationCount;
    }
}

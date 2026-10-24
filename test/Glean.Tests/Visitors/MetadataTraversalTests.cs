using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading;

using Xunit;

using Glean.Contexts;
using Glean.Tests.Utility;
using Glean.Visitors;

namespace Glean.Tests.Visitors;

public sealed class MetadataTraversalTests
{
    private const string TraversalFixtureSource = """
        using System;
        using System.Collections.Generic;

        [assembly: Marker("assembly")]
        [module: Marker("module")]

        [AttributeUsage(AttributeTargets.All)]
        public sealed class MarkerAttribute : Attribute
        {
            public MarkerAttribute(string name)
            {
            }
        }

        namespace Glean.Tests.Visitors
        {
            [Marker("type")]
            public class Outer<T>
            {
                [Marker("field")]
                public int Field;

                [Marker("event")]
                public event Action? Changed;

                [Marker("property")]
                public string Property { get; set; } = string.Empty;

                [Marker("method")]
                public void Run<[Marker("method-generic")] U>([Marker("parameter")] int value)
                {
                    List<int> local = new();
                    Generic<int>(value);
                    Changed?.Invoke();
                }

                public void Generic<V>(V value)
                {
                }

                [Marker("nested-type")]
                public class Inner
                {
                    [Marker("nested-method")]
                    public void Nested()
                    {
                    }
                }
            }
        }
        """;

    [Fact]
    public void Traverse_VisitsStandardProfile()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new CountingVisitor();

        MetadataTraversal.Traverse(metadata.Reader, ref visitor);

        Assert.Equal(CountTopLevelTypes(metadata.Reader), visitor.RootTypeCount);
        Assert.Equal(metadata.Reader.TypeDefinitions.Count, visitor.TypeCount);
        Assert.Equal(metadata.Reader.MethodDefinitions.Count, visitor.MethodCount);
        Assert.Equal(metadata.Reader.FieldDefinitions.Count, visitor.FieldCount);
        Assert.Equal(metadata.Reader.PropertyDefinitions.Count, visitor.PropertyCount);
        Assert.Equal(metadata.Reader.EventDefinitions.Count, visitor.EventCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.Param), visitor.ParameterCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.GenericParam), visitor.GenericParameterCount);
        Assert.Equal(metadata.Reader.CustomAttributes.Count, visitor.CustomAttributeCount);
        Assert.Equal(metadata.Reader.TypeReferences.Count, visitor.TypeReferenceCount);
        Assert.Equal(metadata.Reader.MemberReferences.Count, visitor.MemberReferenceCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.MethodImpl), visitor.MethodImplementationCount);
        Assert.Equal(metadata.Reader.ExportedTypes.Count, visitor.ExportedTypeCount);
        Assert.Equal(metadata.Reader.ManifestResources.Count, visitor.ManifestResourceCount);
    }

    [Fact]
    public void TraverseMethodsOnly_VisitsOnlyMethodCentricProfile()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new CountingVisitor();

        MetadataTraversal.TraverseMethodsOnly(metadata.Reader, ref visitor);

        Assert.Equal(metadata.Reader.TypeDefinitions.Count, visitor.TypeCount);
        Assert.Equal(metadata.Reader.MethodDefinitions.Count, visitor.MethodCount);
        Assert.Equal(0, visitor.FieldCount);
        Assert.Equal(0, visitor.PropertyCount);
        Assert.Equal(0, visitor.EventCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.Param), visitor.ParameterCount);
        Assert.Equal(CountMethodGenericParameters(metadata.Reader), visitor.GenericParameterCount);
        Assert.Equal(CountMethodsOnlyCustomAttributes(metadata.Reader), visitor.CustomAttributeCount);
        Assert.Equal(0, visitor.TypeReferenceCount);
        Assert.Equal(0, visitor.MemberReferenceCount);
        Assert.Equal(0, visitor.MethodImplementationCount);
        Assert.Equal(0, visitor.ExportedTypeCount);
        Assert.Equal(0, visitor.ManifestResourceCount);
    }

    [Fact]
    public void TraverseMethodAttributesOnly_VisitsOnlyMethodsAndMethodAttributes()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new CountingVisitor();

        MetadataTraversal.TraverseMethodAttributesOnly(metadata.Reader, ref visitor);

        Assert.Equal(metadata.Reader.TypeDefinitions.Count, visitor.TypeCount);
        Assert.Equal(metadata.Reader.MethodDefinitions.Count, visitor.MethodCount);
        Assert.Equal(0, visitor.FieldCount);
        Assert.Equal(0, visitor.PropertyCount);
        Assert.Equal(0, visitor.EventCount);
        Assert.Equal(0, visitor.ParameterCount);
        Assert.Equal(0, visitor.GenericParameterCount);
        Assert.Equal(CountMethodAttributesOnlyCustomAttributes(metadata.Reader), visitor.CustomAttributeCount);
        Assert.Equal(0, visitor.TypeReferenceCount);
        Assert.Equal(0, visitor.MemberReferenceCount);
        Assert.Equal(0, visitor.MethodImplementationCount);
        Assert.Equal(0, visitor.ExportedTypeCount);
        Assert.Equal(0, visitor.ManifestResourceCount);
    }

    [Fact]
    public void TraverseWithSignatureTables_VisitsSignatureSpecificationTables()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new CountingVisitor();

        MetadataTraversal.TraverseWithSignatureTables(metadata.Reader, ref visitor);

        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.TypeSpec), visitor.TypeSpecificationCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.MethodSpec), visitor.MethodSpecificationCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.StandAloneSig), visitor.StandaloneSignatureCount);
    }

    [Fact]
    public void Traverse_WhenVisitTypeReturnsFalse_SkipsTypeMembersAndNestedTypes()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new SkipTypeVisitor("Glean.Tests.Visitors", "Outer`1");

        MetadataTraversal.Traverse(metadata.Reader, ref visitor);

        Assert.Equal(CountTopLevelTypes(metadata.Reader), visitor.TypeCount);
        Assert.DoesNotContain("Inner", visitor.TypeNames);
        Assert.DoesNotContain("Run", visitor.MethodNames);
        Assert.DoesNotContain("Nested", visitor.MethodNames);
        Assert.DoesNotContain("Field", visitor.FieldNames);
        Assert.DoesNotContain("Property", visitor.PropertyNames);
        Assert.DoesNotContain("Changed", visitor.EventNames);
        Assert.DoesNotContain("value", visitor.ParameterNames);
        Assert.DoesNotContain("T", visitor.GenericParameterNames);
        Assert.DoesNotContain("U", visitor.GenericParameterNames);
    }

    [Fact]
    public void Traverse_StatefulVisitorMutatesCallerOwnedState()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new StatefulCountVisitor();
        var state = new TraversalState();

        MetadataTraversal.Traverse(metadata.Reader, ref visitor, ref state);

        Assert.Equal(metadata.Reader.TypeDefinitions.Count, state.TypeCount);
        Assert.Equal(metadata.Reader.MethodDefinitions.Count, state.MethodCount);
        Assert.Equal(metadata.Reader.CustomAttributes.Count, state.CustomAttributeCount);
    }

    [Fact]
    public void TraverseMethodsOnly_StatefulVisitorMutatesCallerOwnedState()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new StatefulMethodsOnlyCountVisitor();
        var state = new TraversalState();

        MetadataTraversal.TraverseMethodsOnly(metadata.Reader, ref visitor, ref state);

        Assert.Equal(metadata.Reader.TypeDefinitions.Count, state.TypeCount);
        Assert.Equal(metadata.Reader.MethodDefinitions.Count, state.MethodCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.Param), state.ParameterCount);
        Assert.Equal(CountMethodGenericParameters(metadata.Reader), state.GenericParameterCount);
        Assert.Equal(CountMethodsOnlyCustomAttributes(metadata.Reader), state.CustomAttributeCount);
    }

    [Fact]
    public void TraverseMethodAttributesOnly_StatefulVisitorMutatesCallerOwnedState()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new StatefulMethodAttributeCountVisitor();
        var state = new TraversalState();

        MetadataTraversal.TraverseMethodAttributesOnly(metadata.Reader, ref visitor, ref state);

        Assert.Equal(metadata.Reader.TypeDefinitions.Count, state.TypeCount);
        Assert.Equal(metadata.Reader.MethodDefinitions.Count, state.MethodCount);
        Assert.Equal(CountMethodAttributesOnlyCustomAttributes(metadata.Reader), state.CustomAttributeCount);
        Assert.Equal(0, state.ParameterCount);
        Assert.Equal(0, state.GenericParameterCount);
    }

    [Fact]
    public void TraverseWithSignatureTables_StatefulVisitorMutatesCallerOwnedState()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new StatefulSignatureCountVisitor();
        var state = new TraversalState();

        MetadataTraversal.TraverseWithSignatureTables(metadata.Reader, ref visitor, ref state);

        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.TypeSpec), state.TypeSpecificationCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.MethodSpec), state.MethodSpecificationCount);
        Assert.Equal(metadata.Reader.GetTableRowCount(TableIndex.StandAloneSig), state.StandaloneSignatureCount);
    }

    [Fact]
    public void Traverse_HonorsCancellationBetweenTopLevelTypes()
    {
        using var metadata = TestUtility.BuildMetadata(TraversalFixtureSource);

        var visitor = new CountingVisitor();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => MetadataTraversal.Traverse(metadata.Reader, ref visitor, cancellationSource.Token));
    }

    private static int CountTopLevelTypes(MetadataReader reader)
    {
        int count = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(handle);
            if (!typeDefinition.IsNested)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountMethodGenericParameters(MetadataReader reader)
    {
        int methodGenericParameterCount = 0;
        int genericParameterCount = reader.GetTableRowCount(TableIndex.GenericParam);
        for (int rowNumber = 1; rowNumber <= genericParameterCount; rowNumber++)
        {
            var handle = MetadataTokens.GenericParameterHandle(rowNumber);
            var genericParameter = reader.GetGenericParameter(handle);
            if (genericParameter.Parent.Kind == HandleKind.MethodDefinition)
            {
                methodGenericParameterCount++;
            }
        }

        return methodGenericParameterCount;
    }

    private static int CountMethodsOnlyCustomAttributes(MetadataReader reader)
    {
        int count = reader.GetModuleDefinition().GetCustomAttributes().Count;

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            count += method.GetCustomAttributes().Count;
            count += method.GetParameters().CountAttributes(reader);
            count += method.GetGenericParameters().CountAttributes(reader);
        }

        return count;
    }

    private static int CountMethodAttributesOnlyCustomAttributes(MetadataReader reader)
    {
        int count = reader.GetModuleDefinition().GetCustomAttributes().Count;

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            count += method.GetCustomAttributes().Count;
        }

        return count;
    }

    private struct CountingVisitor : IMetadataVisitor, ISignatureMetadataVisitor
    {
        public int RootTypeCount;
        public int TypeCount;
        public int MethodCount;
        public int FieldCount;
        public int PropertyCount;
        public int EventCount;
        public int ParameterCount;
        public int GenericParameterCount;
        public int CustomAttributeCount;
        public int TypeReferenceCount;
        public int MemberReferenceCount;
        public int MethodImplementationCount;
        public int ExportedTypeCount;
        public int ManifestResourceCount;
        public int TypeSpecificationCount;
        public int MethodSpecificationCount;
        public int StandaloneSignatureCount;

        public bool VisitType(in TypeContext context)
        {
            if (!context.IsNested)
            {
                RootTypeCount++;
            }

            TypeCount++;
            return true;
        }

        public void VisitMethod(in MethodContext context)
        {
            MethodCount++;
        }

        public void VisitField(in FieldContext context)
        {
            FieldCount++;
        }

        public void VisitProperty(in PropertyContext context)
        {
            PropertyCount++;
        }

        public void VisitEvent(in EventContext context)
        {
            EventCount++;
        }

        public void VisitParameter(in ParameterContext context)
        {
            ParameterCount++;
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            GenericParameterCount++;
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
            CustomAttributeCount++;
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
            TypeReferenceCount++;
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
            MemberReferenceCount++;
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
            MethodImplementationCount++;
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
            ExportedTypeCount++;
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
            ManifestResourceCount++;
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
    }

    private struct SkipTypeVisitor : IMetadataVisitor
    {
        private readonly string _namespace;
        private readonly string _name;

        public SkipTypeVisitor(string ns, string name)
        {
            _namespace = ns;
            _name = name;
            TypeCount = 0;
            TypeNames = new List<string>();
            MethodNames = new List<string>();
            FieldNames = new List<string>();
            PropertyNames = new List<string>();
            EventNames = new List<string>();
            ParameterNames = new List<string>();
            GenericParameterNames = new List<string>();
        }

        public int TypeCount { get; private set; }
        public List<string> TypeNames { get; }
        public List<string> MethodNames { get; }
        public List<string> FieldNames { get; }
        public List<string> PropertyNames { get; }
        public List<string> EventNames { get; }
        public List<string> ParameterNames { get; }
        public List<string> GenericParameterNames { get; }

        public bool VisitType(in TypeContext context)
        {
            TypeCount++;
            TypeNames.Add(context.Name);
            return !context.Is(_namespace, _name);
        }

        public void VisitMethod(in MethodContext context)
        {
            MethodNames.Add(context.Name);
        }

        public void VisitField(in FieldContext context)
        {
            FieldNames.Add(context.Name);
        }

        public void VisitProperty(in PropertyContext context)
        {
            PropertyNames.Add(context.Name);
        }

        public void VisitEvent(in EventContext context)
        {
            EventNames.Add(context.Name);
        }

        public void VisitParameter(in ParameterContext context)
        {
            ParameterNames.Add(context.Name);
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            GenericParameterNames.Add(context.Name);
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

    private struct StatefulCountVisitor : IStatefulMetadataVisitor<TraversalState>
    {
        public bool VisitType(in TypeContext context, ref TraversalState state)
        {
            state.TypeCount++;
            return true;
        }

        public void VisitMethod(in MethodContext context, ref TraversalState state)
        {
            state.MethodCount++;
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref TraversalState state)
        {
            state.CustomAttributeCount++;
        }

        public void VisitField(in FieldContext context, ref TraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref TraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref TraversalState state)
        {
        }

        public void VisitParameter(in ParameterContext context, ref TraversalState state)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref TraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref TraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref TraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref TraversalState state)
        {
        }
    }

    private struct StatefulMethodsOnlyCountVisitor : IStatefulMetadataVisitor<TraversalState>
    {
        public bool VisitType(in TypeContext context, ref TraversalState state)
        {
            state.TypeCount++;
            return true;
        }

        public void VisitMethod(in MethodContext context, ref TraversalState state)
        {
            state.MethodCount++;
        }

        public void VisitParameter(in ParameterContext context, ref TraversalState state)
        {
            state.ParameterCount++;
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref TraversalState state)
        {
            state.GenericParameterCount++;
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref TraversalState state)
        {
            state.CustomAttributeCount++;
        }

        public void VisitField(in FieldContext context, ref TraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref TraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref TraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref TraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref TraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref TraversalState state)
        {
        }
    }

    private struct StatefulMethodAttributeCountVisitor : IStatefulMetadataVisitor<TraversalState>
    {
        public bool VisitType(in TypeContext context, ref TraversalState state)
        {
            state.TypeCount++;
            return true;
        }

        public void VisitMethod(in MethodContext context, ref TraversalState state)
        {
            state.MethodCount++;
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref TraversalState state)
        {
            state.CustomAttributeCount++;
        }

        public void VisitField(in FieldContext context, ref TraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref TraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref TraversalState state)
        {
        }

        public void VisitParameter(in ParameterContext context, ref TraversalState state)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref TraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref TraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref TraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref TraversalState state)
        {
        }
    }

    private struct StatefulSignatureCountVisitor :
        IStatefulMetadataVisitor<TraversalState>,
        IStatefulSignatureMetadataVisitor<TraversalState>
    {
        public bool VisitType(in TypeContext context, ref TraversalState state)
        {
            state.TypeCount++;
            return true;
        }

        public void VisitTypeSpecification(in TypeSpecification typeSpecification, TypeSpecificationHandle handle, ref TraversalState state)
        {
            state.TypeSpecificationCount++;
        }

        public void VisitMethodSpecification(in MethodSpecification methodSpecification, MethodSpecificationHandle handle, ref TraversalState state)
        {
            state.MethodSpecificationCount++;
        }

        public void VisitStandaloneSignature(in StandaloneSignature signature, StandaloneSignatureHandle handle, ref TraversalState state)
        {
            state.StandaloneSignatureCount++;
        }

        public void VisitMethod(in MethodContext context, ref TraversalState state)
        {
        }

        public void VisitField(in FieldContext context, ref TraversalState state)
        {
        }

        public void VisitProperty(in PropertyContext context, ref TraversalState state)
        {
        }

        public void VisitEvent(in EventContext context, ref TraversalState state)
        {
        }

        public void VisitParameter(in ParameterContext context, ref TraversalState state)
        {
        }

        public void VisitGenericParameter(in GenericParameterContext context, ref TraversalState state)
        {
        }

        public void VisitCustomAttribute(in CustomAttributeContext context, ref TraversalState state)
        {
        }

        public void VisitTypeReference(in TypeReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMemberReference(in MemberReferenceContext context, ref TraversalState state)
        {
        }

        public void VisitMethodImplementation(in MethodImplementationContext context, ref TraversalState state)
        {
        }

        public void VisitExportedType(in ExportedTypeContext context, ref TraversalState state)
        {
        }

        public void VisitManifestResource(in ManifestResourceContext context, ref TraversalState state)
        {
        }
    }

    private sealed class TraversalState
    {
        public int TypeCount;
        public int MethodCount;
        public int ParameterCount;
        public int GenericParameterCount;
        public int CustomAttributeCount;
        public int TypeSpecificationCount;
        public int MethodSpecificationCount;
        public int StandaloneSignatureCount;
    }
}

internal static class GenericParameterHandleCollectionExtensions
{
    internal static int CountAttributes(this GenericParameterHandleCollection handles, MetadataReader reader)
    {
        int count = 0;
        foreach (var handle in handles)
        {
            count += reader.GetGenericParameter(handle).GetCustomAttributes().Count;
        }

        return count;
    }
}

internal static class ParameterHandleCollectionExtensions
{
    internal static int CountAttributes(this ParameterHandleCollection handles, MetadataReader reader)
    {
        int count = 0;
        foreach (var handle in handles)
        {
            count += reader.GetParameter(handle).GetCustomAttributes().Count;
        }

        return count;
    }
}

using System;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading;

using Glean.Contexts;
using Glean.Enumerators;
using Glean.Extensions;

namespace Glean.Visitors;

/// <summary>
/// Fast metadata traversal using generic struct visitors.
/// </summary>
/// <remarks>
/// Start with <see cref="Traverse{TVisitor}(MetadataReader, ref TVisitor, CancellationToken)"/> for the
/// standard profile. Use the narrower method profiles when you want less coverage, or the signature
/// table profile when you need TypeSpec, MethodSpec, and StandaloneSig callbacks too.
/// Each canonical traversal method also has a stateful overload that passes caller owned state by reference.
/// Stateful overloads preserve visitor and state updates when traversal throws or is cancelled.
/// </remarks>
public static class MetadataTraversal
{
    /// <summary>
    /// Traverses the standard profile plus assembly, module, and file tables.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseWithAssemblyReferences<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IMetadataVisitor, IAssemblyReferenceVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: true);
        TraverseExportedTypesAndManifestResources(reader, ref visitor);
        TraverseAssemblyReferenceTables(reader, ref visitor);
        TraverseTypeAndMemberReferences(reader, ref visitor);
        TraverseTypesAndMembers(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses the standard profile plus assembly, module, and file tables with caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseWithAssemblyReferences<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulMetadataVisitor<TState>, IStatefulAssemblyReferenceVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulAssemblyReferenceMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: true);
            TraverseExportedTypesAndManifestResources(reader, ref adapter);
            TraverseAssemblyReferenceTables(reader, ref adapter);
            TraverseTypeAndMemberReferences(reader, ref adapter);
            TraverseTypesAndMembers(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    /// <summary>
    /// Traverses metadata using the standard profile.
    /// </summary>
    /// <remarks>
    /// Visits assembly and module custom attributes, exported types, manifest resources, type
    /// references, member references, and all top level types recursively with their members,
    /// nested types, generic parameters, custom attributes, and method implementations.
    /// </remarks>
    public static void Traverse<TVisitor>(MetadataReader reader, ref TVisitor visitor, CancellationToken cancellationToken = default)
        where TVisitor : struct, IMetadataVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: true);
        TraverseExportedTypesAndManifestResources(reader, ref visitor);
        TraverseTypeAndMemberReferences(reader, ref visitor);
        TraverseTypesAndMembers(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses metadata using the standard profile and caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void Traverse<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulMetadataVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: true);
            TraverseExportedTypesAndManifestResources(reader, ref adapter);
            TraverseTypeAndMemberReferences(reader, ref adapter);
            TraverseTypesAndMembers(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    /// <summary>
    /// Traverses only types, methods, parameters, method generic parameters, and related attributes.
    /// </summary>
    public static void TraverseMethodsOnly<TVisitor>(MetadataReader reader, ref TVisitor visitor, CancellationToken cancellationToken = default)
        where TVisitor : struct, IMetadataVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: false);
        TraverseTypesMethodsOnly(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses only types, methods, parameters, method generic parameters, and related attributes with caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseMethodsOnly<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulMetadataVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: false);
            TraverseTypesMethodsOnly(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    /// <summary>
    /// Traverses only types, methods, and method level custom attributes.
    /// </summary>
    public static void TraverseMethodAttributesOnly<TVisitor>(MetadataReader reader, ref TVisitor visitor, CancellationToken cancellationToken = default)
        where TVisitor : struct, IMetadataVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: false);
        TraverseTypesMethodAttributesOnly(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses only types, methods, and method level custom attributes with caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseMethodAttributesOnly<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulMetadataVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: false);
            TraverseTypesMethodAttributesOnly(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    /// <summary>
    /// Traverses the standard profile plus signature specification tables.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseWithSignatureTables<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IMetadataVisitor, ISignatureMetadataVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: true);
        TraverseExportedTypesAndManifestResources(reader, ref visitor);
        TraverseTypeAndMemberReferences(reader, ref visitor);
        TraverseSignatureTables(reader, ref visitor);
        TraverseTypesAndMembers(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses the standard profile plus signature specification tables with caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseWithSignatureTables<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulMetadataVisitor<TState>, IStatefulSignatureMetadataVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulSignatureMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: true);
            TraverseExportedTypesAndManifestResources(reader, ref adapter);
            TraverseTypeAndMemberReferences(reader, ref adapter);
            TraverseSignatureTables(reader, ref adapter);
            TraverseTypesAndMembers(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    /// <summary>
    /// Traverses the standard profile plus constant, declarative security, and custom debug information tables.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseWithConstantAndDebugTables<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IMetadataVisitor, IConstantAndDebugMetadataVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: true);
        TraverseExportedTypesAndManifestResources(reader, ref visitor);
        TraverseTypeAndMemberReferences(reader, ref visitor);
        TraverseConstantAndDebugTables(reader, ref visitor);
        TraverseTypesAndMembers(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses the standard profile plus constant, declarative security, and custom debug information tables with caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseWithConstantAndDebugTables<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulMetadataVisitor<TState>, IStatefulConstantAndDebugMetadataVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulConstantAndDebugMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: true);
            TraverseExportedTypesAndManifestResources(reader, ref adapter);
            TraverseTypeAndMemberReferences(reader, ref adapter);
            TraverseConstantAndDebugTables(reader, ref adapter);
            TraverseTypesAndMembers(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    /// <summary>
    /// Traverses the standard profile plus all advanced metadata tables.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseAllTables<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, ICompleteMetadataVisitor
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        TraverseAssemblyAndModuleAttributes(reader, ref visitor, includeAssemblyAttributes: true);
        TraverseExportedTypesAndManifestResources(reader, ref visitor);
        TraverseAssemblyReferenceTables(reader, ref visitor);
        TraverseTypeAndMemberReferences(reader, ref visitor);
        TraverseSignatureTables(reader, ref visitor);
        TraverseConstantAndDebugTables(reader, ref visitor);
        TraverseTypesAndMembers(reader, ref visitor, cancellationToken);
    }

    /// <summary>
    /// Traverses the standard profile plus all advanced metadata tables with caller owned state.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void TraverseAllTables<TVisitor, TState>(
        MetadataReader reader,
        ref TVisitor visitor,
        ref TState state,
        CancellationToken cancellationToken = default)
        where TVisitor : struct, IStatefulCompleteMetadataVisitor<TState>
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var adapter = new StatefulCompleteMetadataVisitorAdapter<TVisitor, TState>
        {
            Visitor = visitor,
            State = state
        };

        try
        {
            TraverseAssemblyAndModuleAttributes(reader, ref adapter, includeAssemblyAttributes: true);
            TraverseExportedTypesAndManifestResources(reader, ref adapter);
            TraverseAssemblyReferenceTables(reader, ref adapter);
            TraverseTypeAndMemberReferences(reader, ref adapter);
            TraverseSignatureTables(reader, ref adapter);
            TraverseConstantAndDebugTables(reader, ref adapter);
            TraverseTypesAndMembers(reader, ref adapter, cancellationToken);
        }
        finally
        {
            visitor = adapter.Visitor;
            state = adapter.State;
        }
    }

    private static void TraverseAssemblyAndModuleAttributes<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        bool includeAssemblyAttributes)
        where TVisitor : struct, IMetadataVisitor
    {
        if (includeAssemblyAttributes && reader.IsAssembly)
        {
            var assembly = AssemblyContext.Create(reader);
            TraverseCustomAttributes(assembly.EnumerateCustomAttributes(), ref visitor);
        }

        var moduleDefinition = reader.GetModuleDefinition();
        TraverseCustomAttributeHandles(reader, moduleDefinition.GetCustomAttributes(), ref visitor);
    }

    private static void TraverseExportedTypesAndManifestResources<TVisitor>(MetadataReader reader, ref TVisitor visitor)
        where TVisitor : struct, IMetadataVisitor
    {
        if (reader.IsAssembly)
        {
            var assembly = AssemblyContext.Create(reader);

            foreach (var exportedType in assembly.EnumerateExportedTypes())
            {
                visitor.VisitExportedType(in exportedType);
            }

            foreach (var manifestResource in assembly.EnumerateManifestResources())
            {
                visitor.VisitManifestResource(in manifestResource);
            }
        }
        else
        {
            foreach (var handle in reader.ExportedTypes)
            {
                var exportedType = ExportedTypeContext.Create(reader, handle);
                visitor.VisitExportedType(in exportedType);
            }

            foreach (var handle in reader.ManifestResources)
            {
                var manifestResource = ManifestResourceContext.Create(reader, handle);
                visitor.VisitManifestResource(in manifestResource);
            }
        }
    }

    private static void TraverseTypeAndMemberReferences<TVisitor>(MetadataReader reader, ref TVisitor visitor)
        where TVisitor : struct, IMetadataVisitor
    {
        foreach (var typeReference in reader.EnumerateTypeReferences())
        {
            visitor.VisitTypeReference(in typeReference);
        }

        foreach (var memberReference in reader.EnumerateMemberReferences())
        {
            visitor.VisitMemberReference(in memberReference);
        }
    }

    private static void TraverseAssemblyReferenceTables<TVisitor>(MetadataReader reader, ref TVisitor visitor)
        where TVisitor : struct, IAssemblyReferenceVisitor
    {
        foreach (var assemblyReference in reader.EnumerateAssemblyReferences())
        {
            visitor.VisitAssemblyReference(in assemblyReference);
        }

        foreach (var moduleReference in reader.EnumerateModuleReferences())
        {
            visitor.VisitModuleReference(in moduleReference);
        }

        int assemblyFileCount = reader.GetTableRowCount(TableIndex.File);
        for (int rowNumber = 1; rowNumber <= assemblyFileCount; rowNumber++)
        {
            var handle = MetadataTokens.AssemblyFileHandle(rowNumber);
            var assemblyFile = reader.GetAssemblyFile(handle);
            visitor.VisitAssemblyFile(in assemblyFile, handle);
        }
    }

    private static void TraverseSignatureTables<TVisitor>(MetadataReader reader, ref TVisitor visitor)
        where TVisitor : struct, ISignatureMetadataVisitor
    {
        int typeSpecificationCount = reader.GetTableRowCount(TableIndex.TypeSpec);
        for (int rowNumber = 1; rowNumber <= typeSpecificationCount; rowNumber++)
        {
            var handle = MetadataTokens.TypeSpecificationHandle(rowNumber);
            var typeSpecification = reader.GetTypeSpecification(handle);
            visitor.VisitTypeSpecification(in typeSpecification, handle);
        }

        int methodSpecificationCount = reader.GetTableRowCount(TableIndex.MethodSpec);
        for (int rowNumber = 1; rowNumber <= methodSpecificationCount; rowNumber++)
        {
            var handle = MetadataTokens.MethodSpecificationHandle(rowNumber);
            var methodSpecification = reader.GetMethodSpecification(handle);
            visitor.VisitMethodSpecification(in methodSpecification, handle);
        }

        int standaloneSignatureCount = reader.GetTableRowCount(TableIndex.StandAloneSig);
        for (int rowNumber = 1; rowNumber <= standaloneSignatureCount; rowNumber++)
        {
            var handle = MetadataTokens.StandaloneSignatureHandle(rowNumber);
            var standaloneSignature = reader.GetStandaloneSignature(handle);
            visitor.VisitStandaloneSignature(in standaloneSignature, handle);
        }
    }

    private static void TraverseConstantAndDebugTables<TVisitor>(MetadataReader reader, ref TVisitor visitor)
        where TVisitor : struct, IConstantAndDebugMetadataVisitor
    {
        int constantCount = reader.GetTableRowCount(TableIndex.Constant);
        for (int rowNumber = 1; rowNumber <= constantCount; rowNumber++)
        {
            var handle = MetadataTokens.ConstantHandle(rowNumber);
            var constant = reader.GetConstant(handle);
            visitor.VisitConstant(in constant, handle);
        }

        int declarativeSecurityCount = reader.GetTableRowCount(TableIndex.DeclSecurity);
        for (int rowNumber = 1; rowNumber <= declarativeSecurityCount; rowNumber++)
        {
            var handle = MetadataTokens.DeclarativeSecurityAttributeHandle(rowNumber);
            var declarativeSecurity = reader.GetDeclarativeSecurityAttribute(handle);
            visitor.VisitDeclarativeSecurityAttribute(in declarativeSecurity, handle);
        }

        int customDebugInformationCount = reader.GetTableRowCount(TableIndex.CustomDebugInformation);
        for (int rowNumber = 1; rowNumber <= customDebugInformationCount; rowNumber++)
        {
            var handle = MetadataTokens.CustomDebugInformationHandle(rowNumber);
            var customDebugInformation = reader.GetCustomDebugInformation(handle);
            visitor.VisitCustomDebugInformation(in customDebugInformation, handle);
        }
    }

    private static void TraverseTypesAndMembers<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken)
        where TVisitor : struct, IMetadataVisitor
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var type = TypeContext.Create(reader, handle);
            if (type.IsNested)
            {
                continue;
            }

            TraverseType(ref visitor, in type);
        }
    }

    private static void TraverseTypesMethodsOnly<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken)
        where TVisitor : struct, IMetadataVisitor
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var type = TypeContext.Create(reader, handle);
            if (type.IsNested)
            {
                continue;
            }

            TraverseTypeMethodsOnly(ref visitor, in type);
        }
    }

    private static void TraverseTypesMethodAttributesOnly<TVisitor>(
        MetadataReader reader,
        ref TVisitor visitor,
        CancellationToken cancellationToken)
        where TVisitor : struct, IMetadataVisitor
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var type = TypeContext.Create(reader, handle);
            if (type.IsNested)
            {
                continue;
            }

            TraverseTypeMethodAttributesOnly(ref visitor, in type);
        }
    }

    private static void TraverseType<TVisitor>(ref TVisitor visitor, in TypeContext type)
        where TVisitor : struct, IMetadataVisitor
    {
        if (!visitor.VisitType(in type))
        {
            return;
        }

        TraverseCustomAttributes(type.EnumerateCustomAttributes(), ref visitor);

        foreach (var genericParameter in type.EnumerateGenericParameters())
        {
            visitor.VisitGenericParameter(in genericParameter);
            TraverseCustomAttributes(genericParameter.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var field in type.EnumerateFields())
        {
            visitor.VisitField(in field);
            TraverseCustomAttributes(field.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var method in type.EnumerateMethods())
        {
            TraverseMethod(ref visitor, in method);
        }

        foreach (var property in type.EnumerateProperties())
        {
            visitor.VisitProperty(in property);
            TraverseCustomAttributes(property.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var eventContext in type.EnumerateEvents())
        {
            visitor.VisitEvent(in eventContext);
            TraverseCustomAttributes(eventContext.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var methodImplementation in type.EnumerateMethodImplementations())
        {
            visitor.VisitMethodImplementation(in methodImplementation);
        }

        foreach (var nestedType in type.EnumerateNestedTypes())
        {
            TraverseType(ref visitor, in nestedType);
        }
    }

    private static void TraverseTypeMethodsOnly<TVisitor>(ref TVisitor visitor, in TypeContext type)
        where TVisitor : struct, IMetadataVisitor
    {
        if (!visitor.VisitType(in type))
        {
            return;
        }

        foreach (var method in type.EnumerateMethods())
        {
            TraverseMethodMethodsOnly(ref visitor, in method);
        }

        foreach (var nestedType in type.EnumerateNestedTypes())
        {
            TraverseTypeMethodsOnly(ref visitor, in nestedType);
        }
    }

    private static void TraverseTypeMethodAttributesOnly<TVisitor>(ref TVisitor visitor, in TypeContext type)
        where TVisitor : struct, IMetadataVisitor
    {
        if (!visitor.VisitType(in type))
        {
            return;
        }

        foreach (var method in type.EnumerateMethods())
        {
            visitor.VisitMethod(in method);
            TraverseCustomAttributes(method.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var nestedType in type.EnumerateNestedTypes())
        {
            TraverseTypeMethodAttributesOnly(ref visitor, in nestedType);
        }
    }

    private static void TraverseMethod<TVisitor>(ref TVisitor visitor, in MethodContext method)
        where TVisitor : struct, IMetadataVisitor
    {
        visitor.VisitMethod(in method);
        TraverseCustomAttributes(method.EnumerateCustomAttributes(), ref visitor);

        foreach (var genericParameter in method.EnumerateGenericParameters())
        {
            visitor.VisitGenericParameter(in genericParameter);
            TraverseCustomAttributes(genericParameter.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var parameter in method.EnumerateParameters())
        {
            visitor.VisitParameter(in parameter);
            TraverseCustomAttributes(parameter.EnumerateCustomAttributes(), ref visitor);
        }
    }

    private static void TraverseMethodMethodsOnly<TVisitor>(ref TVisitor visitor, in MethodContext method)
        where TVisitor : struct, IMetadataVisitor
    {
        visitor.VisitMethod(in method);
        TraverseCustomAttributes(method.EnumerateCustomAttributes(), ref visitor);

        foreach (var genericParameter in method.EnumerateGenericParameters())
        {
            visitor.VisitGenericParameter(in genericParameter);
            TraverseCustomAttributes(genericParameter.EnumerateCustomAttributes(), ref visitor);
        }

        foreach (var parameter in method.EnumerateParameters())
        {
            visitor.VisitParameter(in parameter);
            TraverseCustomAttributes(parameter.EnumerateCustomAttributes(), ref visitor);
        }
    }

    private static void TraverseCustomAttributes<TVisitor>(CustomAttributeEnumerator enumerator, ref TVisitor visitor)
        where TVisitor : struct, IMetadataVisitor
    {
        foreach (var attribute in enumerator)
        {
            visitor.VisitCustomAttribute(in attribute);
        }
    }

    private static void TraverseCustomAttributeHandles<TVisitor>(
        MetadataReader reader,
        CustomAttributeHandleCollection attributes,
        ref TVisitor visitor)
        where TVisitor : struct, IMetadataVisitor
    {
        foreach (var handle in attributes)
        {
            var attribute = CustomAttributeContext.Create(reader, handle);
            visitor.VisitCustomAttribute(in attribute);
        }
    }

    private struct StatefulMetadataVisitorAdapter<TVisitor, TState> : IMetadataVisitor
        where TVisitor : struct, IStatefulMetadataVisitor<TState>
    {
        public TVisitor Visitor;
        public TState State;

        public bool VisitType(in TypeContext context)
        {
            return Visitor.VisitType(in context, ref State);
        }

        public void VisitMethod(in MethodContext context)
        {
            Visitor.VisitMethod(in context, ref State);
        }

        public void VisitField(in FieldContext context)
        {
            Visitor.VisitField(in context, ref State);
        }

        public void VisitProperty(in PropertyContext context)
        {
            Visitor.VisitProperty(in context, ref State);
        }

        public void VisitEvent(in EventContext context)
        {
            Visitor.VisitEvent(in context, ref State);
        }

        public void VisitParameter(in ParameterContext context)
        {
            Visitor.VisitParameter(in context, ref State);
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            Visitor.VisitGenericParameter(in context, ref State);
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
            Visitor.VisitCustomAttribute(in context, ref State);
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
            Visitor.VisitTypeReference(in context, ref State);
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
            Visitor.VisitMemberReference(in context, ref State);
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
            Visitor.VisitMethodImplementation(in context, ref State);
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
            Visitor.VisitExportedType(in context, ref State);
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
            Visitor.VisitManifestResource(in context, ref State);
        }
    }

    private struct StatefulAssemblyReferenceMetadataVisitorAdapter<TVisitor, TState> : IMetadataVisitor, IAssemblyReferenceVisitor
        where TVisitor : struct, IStatefulMetadataVisitor<TState>, IStatefulAssemblyReferenceVisitor<TState>
    {
        public TVisitor Visitor;
        public TState State;

        public bool VisitType(in TypeContext context)
        {
            return Visitor.VisitType(in context, ref State);
        }

        public void VisitMethod(in MethodContext context)
        {
            Visitor.VisitMethod(in context, ref State);
        }

        public void VisitField(in FieldContext context)
        {
            Visitor.VisitField(in context, ref State);
        }

        public void VisitProperty(in PropertyContext context)
        {
            Visitor.VisitProperty(in context, ref State);
        }

        public void VisitEvent(in EventContext context)
        {
            Visitor.VisitEvent(in context, ref State);
        }

        public void VisitParameter(in ParameterContext context)
        {
            Visitor.VisitParameter(in context, ref State);
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            Visitor.VisitGenericParameter(in context, ref State);
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
            Visitor.VisitCustomAttribute(in context, ref State);
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
            Visitor.VisitTypeReference(in context, ref State);
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
            Visitor.VisitMemberReference(in context, ref State);
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
            Visitor.VisitMethodImplementation(in context, ref State);
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
            Visitor.VisitExportedType(in context, ref State);
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
            Visitor.VisitManifestResource(in context, ref State);
        }

        public void VisitAssemblyReference(in AssemblyReferenceContext context)
        {
            Visitor.VisitAssemblyReference(in context, ref State);
        }

        public void VisitModuleReference(in ModuleReferenceContext context)
        {
            Visitor.VisitModuleReference(in context, ref State);
        }

        public void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle)
        {
            Visitor.VisitAssemblyFile(in file, handle, ref State);
        }
    }

    private struct StatefulSignatureMetadataVisitorAdapter<TVisitor, TState> : IMetadataVisitor, ISignatureMetadataVisitor
        where TVisitor : struct, IStatefulMetadataVisitor<TState>, IStatefulSignatureMetadataVisitor<TState>
    {
        public TVisitor Visitor;
        public TState State;

        public bool VisitType(in TypeContext context)
        {
            return Visitor.VisitType(in context, ref State);
        }

        public void VisitMethod(in MethodContext context)
        {
            Visitor.VisitMethod(in context, ref State);
        }

        public void VisitField(in FieldContext context)
        {
            Visitor.VisitField(in context, ref State);
        }

        public void VisitProperty(in PropertyContext context)
        {
            Visitor.VisitProperty(in context, ref State);
        }

        public void VisitEvent(in EventContext context)
        {
            Visitor.VisitEvent(in context, ref State);
        }

        public void VisitParameter(in ParameterContext context)
        {
            Visitor.VisitParameter(in context, ref State);
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            Visitor.VisitGenericParameter(in context, ref State);
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
            Visitor.VisitCustomAttribute(in context, ref State);
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
            Visitor.VisitTypeReference(in context, ref State);
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
            Visitor.VisitMemberReference(in context, ref State);
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
            Visitor.VisitMethodImplementation(in context, ref State);
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
            Visitor.VisitExportedType(in context, ref State);
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
            Visitor.VisitManifestResource(in context, ref State);
        }

        public void VisitTypeSpecification(in TypeSpecification typeSpecification, TypeSpecificationHandle handle)
        {
            Visitor.VisitTypeSpecification(in typeSpecification, handle, ref State);
        }

        public void VisitMethodSpecification(in MethodSpecification methodSpecification, MethodSpecificationHandle handle)
        {
            Visitor.VisitMethodSpecification(in methodSpecification, handle, ref State);
        }

        public void VisitStandaloneSignature(in StandaloneSignature signature, StandaloneSignatureHandle handle)
        {
            Visitor.VisitStandaloneSignature(in signature, handle, ref State);
        }
    }

    private struct StatefulConstantAndDebugMetadataVisitorAdapter<TVisitor, TState> : IMetadataVisitor, IConstantAndDebugMetadataVisitor
        where TVisitor : struct, IStatefulMetadataVisitor<TState>, IStatefulConstantAndDebugMetadataVisitor<TState>
    {
        public TVisitor Visitor;
        public TState State;

        public bool VisitType(in TypeContext context)
        {
            return Visitor.VisitType(in context, ref State);
        }

        public void VisitMethod(in MethodContext context)
        {
            Visitor.VisitMethod(in context, ref State);
        }

        public void VisitField(in FieldContext context)
        {
            Visitor.VisitField(in context, ref State);
        }

        public void VisitProperty(in PropertyContext context)
        {
            Visitor.VisitProperty(in context, ref State);
        }

        public void VisitEvent(in EventContext context)
        {
            Visitor.VisitEvent(in context, ref State);
        }

        public void VisitParameter(in ParameterContext context)
        {
            Visitor.VisitParameter(in context, ref State);
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            Visitor.VisitGenericParameter(in context, ref State);
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
            Visitor.VisitCustomAttribute(in context, ref State);
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
            Visitor.VisitTypeReference(in context, ref State);
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
            Visitor.VisitMemberReference(in context, ref State);
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
            Visitor.VisitMethodImplementation(in context, ref State);
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
            Visitor.VisitExportedType(in context, ref State);
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
            Visitor.VisitManifestResource(in context, ref State);
        }

        public void VisitConstant(in Constant constant, ConstantHandle handle)
        {
            Visitor.VisitConstant(in constant, handle, ref State);
        }

        public void VisitDeclarativeSecurityAttribute(
            in DeclarativeSecurityAttribute attribute,
            DeclarativeSecurityAttributeHandle handle)
        {
            Visitor.VisitDeclarativeSecurityAttribute(in attribute, handle, ref State);
        }

        public void VisitCustomDebugInformation(in CustomDebugInformation info, CustomDebugInformationHandle handle)
        {
            Visitor.VisitCustomDebugInformation(in info, handle, ref State);
        }
    }

    private struct StatefulCompleteMetadataVisitorAdapter<TVisitor, TState> :
        IMetadataVisitor,
        IAssemblyReferenceVisitor,
        ISignatureMetadataVisitor,
        IConstantAndDebugMetadataVisitor
        where TVisitor : struct, IStatefulCompleteMetadataVisitor<TState>
    {
        public TVisitor Visitor;
        public TState State;

        public bool VisitType(in TypeContext context)
        {
            return Visitor.VisitType(in context, ref State);
        }

        public void VisitMethod(in MethodContext context)
        {
            Visitor.VisitMethod(in context, ref State);
        }

        public void VisitField(in FieldContext context)
        {
            Visitor.VisitField(in context, ref State);
        }

        public void VisitProperty(in PropertyContext context)
        {
            Visitor.VisitProperty(in context, ref State);
        }

        public void VisitEvent(in EventContext context)
        {
            Visitor.VisitEvent(in context, ref State);
        }

        public void VisitParameter(in ParameterContext context)
        {
            Visitor.VisitParameter(in context, ref State);
        }

        public void VisitGenericParameter(in GenericParameterContext context)
        {
            Visitor.VisitGenericParameter(in context, ref State);
        }

        public void VisitCustomAttribute(in CustomAttributeContext context)
        {
            Visitor.VisitCustomAttribute(in context, ref State);
        }

        public void VisitTypeReference(in TypeReferenceContext context)
        {
            Visitor.VisitTypeReference(in context, ref State);
        }

        public void VisitMemberReference(in MemberReferenceContext context)
        {
            Visitor.VisitMemberReference(in context, ref State);
        }

        public void VisitMethodImplementation(in MethodImplementationContext context)
        {
            Visitor.VisitMethodImplementation(in context, ref State);
        }

        public void VisitExportedType(in ExportedTypeContext context)
        {
            Visitor.VisitExportedType(in context, ref State);
        }

        public void VisitManifestResource(in ManifestResourceContext context)
        {
            Visitor.VisitManifestResource(in context, ref State);
        }

        public void VisitAssemblyReference(in AssemblyReferenceContext context)
        {
            Visitor.VisitAssemblyReference(in context, ref State);
        }

        public void VisitModuleReference(in ModuleReferenceContext context)
        {
            Visitor.VisitModuleReference(in context, ref State);
        }

        public void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle)
        {
            Visitor.VisitAssemblyFile(in file, handle, ref State);
        }

        public void VisitTypeSpecification(in TypeSpecification typeSpecification, TypeSpecificationHandle handle)
        {
            Visitor.VisitTypeSpecification(in typeSpecification, handle, ref State);
        }

        public void VisitMethodSpecification(in MethodSpecification methodSpecification, MethodSpecificationHandle handle)
        {
            Visitor.VisitMethodSpecification(in methodSpecification, handle, ref State);
        }

        public void VisitStandaloneSignature(in StandaloneSignature signature, StandaloneSignatureHandle handle)
        {
            Visitor.VisitStandaloneSignature(in signature, handle, ref State);
        }

        public void VisitConstant(in Constant constant, ConstantHandle handle)
        {
            Visitor.VisitConstant(in constant, handle, ref State);
        }

        public void VisitDeclarativeSecurityAttribute(
            in DeclarativeSecurityAttribute attribute,
            DeclarativeSecurityAttributeHandle handle)
        {
            Visitor.VisitDeclarativeSecurityAttribute(in attribute, handle, ref State);
        }

        public void VisitCustomDebugInformation(in CustomDebugInformation info, CustomDebugInformationHandle handle)
        {
            Visitor.VisitCustomDebugInformation(in info, handle, ref State);
        }
    }
}

using System.ComponentModel;
using System.Reflection.Metadata;

using Glean.Contexts;

namespace Glean.Visitors;

/// <summary>
/// Visitor interface for assembly and module reference tables.
/// </summary>
/// <remarks>
/// Coverage is optional. Combine this with <see cref="IMetadataVisitor"/> and drive it with
/// <see cref="MetadataTraversal.TraverseWithAssemblyReferences{TVisitor}(MetadataReader, ref TVisitor, CancellationToken)"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IAssemblyReferenceVisitor
{
    /// <summary>
    /// Called for each assembly reference.
    /// </summary>
    void VisitAssemblyReference(in AssemblyReferenceContext context);

    /// <summary>
    /// Called for each module reference.
    /// </summary>
    void VisitModuleReference(in ModuleReferenceContext context);

    /// <summary>
    /// Called for each file row in a multi file assembly.
    /// </summary>
    void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle);
}

/// <summary>
/// Stateful visitor interface for assembly and module reference tables.
/// </summary>
/// <typeparam name="TState">The caller owned state type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IStatefulAssemblyReferenceVisitor<TState>
{
    /// <summary>
    /// Called for each assembly reference.
    /// </summary>
    void VisitAssemblyReference(in AssemblyReferenceContext context, ref TState state);

    /// <summary>
    /// Called for each module reference.
    /// </summary>
    void VisitModuleReference(in ModuleReferenceContext context, ref TState state);

    /// <summary>
    /// Called for each file row in a multi file assembly.
    /// </summary>
    void VisitAssemblyFile(in AssemblyFile file, AssemblyFileHandle handle, ref TState state);
}

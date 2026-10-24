using System.ComponentModel;
using System.Reflection.Metadata;

namespace Glean.Visitors;

/// <summary>
/// Visitor interface for signature specification tables.
/// </summary>
/// <remarks>
/// Coverage is optional. Combine this with <see cref="IMetadataVisitor"/> and drive it with
/// <see cref="MetadataTraversal.TraverseWithSignatureTables{TVisitor}(MetadataReader, ref TVisitor, CancellationToken)"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ISignatureMetadataVisitor
{
    /// <summary>
    /// Called for each type specification.
    /// </summary>
    void VisitTypeSpecification(in TypeSpecification typeSpecification, TypeSpecificationHandle handle);

    /// <summary>
    /// Called for each method specification.
    /// </summary>
    void VisitMethodSpecification(in MethodSpecification methodSpecification, MethodSpecificationHandle handle);

    /// <summary>
    /// Called for each standalone signature.
    /// </summary>
    void VisitStandaloneSignature(in StandaloneSignature signature, StandaloneSignatureHandle handle);
}

/// <summary>
/// Stateful visitor interface for signature specification tables.
/// </summary>
/// <typeparam name="TState">The caller owned state type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IStatefulSignatureMetadataVisitor<TState>
{
    /// <summary>
    /// Called for each type specification.
    /// </summary>
    void VisitTypeSpecification(in TypeSpecification typeSpecification, TypeSpecificationHandle handle, ref TState state);

    /// <summary>
    /// Called for each method specification.
    /// </summary>
    void VisitMethodSpecification(in MethodSpecification methodSpecification, MethodSpecificationHandle handle, ref TState state);

    /// <summary>
    /// Called for each standalone signature.
    /// </summary>
    void VisitStandaloneSignature(in StandaloneSignature signature, StandaloneSignatureHandle handle, ref TState state);
}

using System.ComponentModel;

namespace Glean.Visitors;

/// <summary>
/// Visitor interface that includes all optional advanced metadata table coverage.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ICompleteMetadataVisitor :
    IMetadataVisitor,
    IAssemblyReferenceVisitor,
    ISignatureMetadataVisitor,
    IConstantAndDebugMetadataVisitor
{
}

/// <summary>
/// Stateful visitor interface that includes all optional advanced metadata table coverage.
/// </summary>
/// <typeparam name="TState">The caller owned state type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IStatefulCompleteMetadataVisitor<TState> :
    IStatefulMetadataVisitor<TState>,
    IStatefulAssemblyReferenceVisitor<TState>,
    IStatefulSignatureMetadataVisitor<TState>,
    IStatefulConstantAndDebugMetadataVisitor<TState>
{
}

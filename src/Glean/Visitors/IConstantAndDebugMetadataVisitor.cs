using System.ComponentModel;
using System.Reflection.Metadata;

namespace Glean.Visitors;

/// <summary>
/// Visitor interface for constant, declarative security, and custom debug information tables.
/// </summary>
/// <remarks>
/// Coverage is optional. Combine this with <see cref="IMetadataVisitor"/> and drive it with
/// <see cref="MetadataTraversal.TraverseWithConstantAndDebugTables{TVisitor}(MetadataReader, ref TVisitor, CancellationToken)"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IConstantAndDebugMetadataVisitor
{
    /// <summary>
    /// Called for each constant row.
    /// </summary>
    void VisitConstant(in Constant constant, ConstantHandle handle);

    /// <summary>
    /// Called for each declarative security row.
    /// </summary>
    void VisitDeclarativeSecurityAttribute(in DeclarativeSecurityAttribute attribute, DeclarativeSecurityAttributeHandle handle);

    /// <summary>
    /// Called for each custom debug information row.
    /// </summary>
    void VisitCustomDebugInformation(in CustomDebugInformation info, CustomDebugInformationHandle handle);
}

/// <summary>
/// Stateful visitor interface for constant, declarative security, and custom debug information tables.
/// </summary>
/// <typeparam name="TState">The caller owned state type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IStatefulConstantAndDebugMetadataVisitor<TState>
{
    /// <summary>
    /// Called for each constant row.
    /// </summary>
    void VisitConstant(in Constant constant, ConstantHandle handle, ref TState state);

    /// <summary>
    /// Called for each declarative security row.
    /// </summary>
    void VisitDeclarativeSecurityAttribute(
        in DeclarativeSecurityAttribute attribute,
        DeclarativeSecurityAttributeHandle handle,
        ref TState state);

    /// <summary>
    /// Called for each custom debug information row.
    /// </summary>
    void VisitCustomDebugInformation(in CustomDebugInformation info, CustomDebugInformationHandle handle, ref TState state);
}

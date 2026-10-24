using System.ComponentModel;

using Glean.Contexts;

namespace Glean.Visitors;

/// <summary>
/// Generic visitor interface for fast metadata traversal.
/// </summary>
/// <remarks>
/// All context parameters are passed by readonly reference. Implement every callback
/// explicitly, using empty bodies for unwanted callbacks, to avoid boxing struct visitors.
/// </remarks>
public interface IMetadataVisitor
{
    /// <summary>
    /// Called when a type is visited. Return false to skip its nested members and nested types.
    /// </summary>
    bool VisitType(in TypeContext context);

    /// <summary>
    /// Called when a method is visited.
    /// </summary>
    void VisitMethod(in MethodContext context);

    /// <summary>
    /// Called when a field is visited.
    /// </summary>
    void VisitField(in FieldContext context);

    /// <summary>
    /// Called when a property is visited.
    /// </summary>
    void VisitProperty(in PropertyContext context);

    /// <summary>
    /// Called when an event is visited.
    /// </summary>
    void VisitEvent(in EventContext context);

    /// <summary>
    /// Called when a method parameter is visited.
    /// </summary>
    void VisitParameter(in ParameterContext context);

    /// <summary>
    /// Called when a generic parameter is visited.
    /// </summary>
    void VisitGenericParameter(in GenericParameterContext context);

    /// <summary>
    /// Called when a custom attribute is visited.
    /// </summary>
    void VisitCustomAttribute(in CustomAttributeContext context);

    /// <summary>
    /// Called when a type reference is visited.
    /// </summary>
    void VisitTypeReference(in TypeReferenceContext context);

    /// <summary>
    /// Called when a member reference is visited.
    /// </summary>
    void VisitMemberReference(in MemberReferenceContext context);

    /// <summary>
    /// Called when a method implementation is visited.
    /// </summary>
    void VisitMethodImplementation(in MethodImplementationContext context);

    /// <summary>
    /// Called when an exported type is visited.
    /// </summary>
    void VisitExportedType(in ExportedTypeContext context);

    /// <summary>
    /// Called when a manifest resource is visited.
    /// </summary>
    void VisitManifestResource(in ManifestResourceContext context);
}

/// <summary>
/// Stateful fast metadata visitor interface with explicit state passing.
/// </summary>
/// <typeparam name="TState">The caller owned state type.</typeparam>
/// <remarks>
/// Like <see cref="IMetadataVisitor"/>, but passes caller owned state by reference.
/// Useful when you want visitor logic separated from aggregation state.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IStatefulMetadataVisitor<TState>
{
    /// <summary>
    /// Called when a type is visited. Return false to skip its nested members and nested types.
    /// </summary>
    bool VisitType(in TypeContext context, ref TState state);

    /// <summary>
    /// Called when a method is visited.
    /// </summary>
    void VisitMethod(in MethodContext context, ref TState state);

    /// <summary>
    /// Called when a field is visited.
    /// </summary>
    void VisitField(in FieldContext context, ref TState state);

    /// <summary>
    /// Called when a property is visited.
    /// </summary>
    void VisitProperty(in PropertyContext context, ref TState state);

    /// <summary>
    /// Called when an event is visited.
    /// </summary>
    void VisitEvent(in EventContext context, ref TState state);

    /// <summary>
    /// Called when a method parameter is visited.
    /// </summary>
    void VisitParameter(in ParameterContext context, ref TState state);

    /// <summary>
    /// Called when a generic parameter is visited.
    /// </summary>
    void VisitGenericParameter(in GenericParameterContext context, ref TState state);

    /// <summary>
    /// Called when a custom attribute is visited.
    /// </summary>
    void VisitCustomAttribute(in CustomAttributeContext context, ref TState state);

    /// <summary>
    /// Called when a type reference is visited.
    /// </summary>
    void VisitTypeReference(in TypeReferenceContext context, ref TState state);

    /// <summary>
    /// Called when a member reference is visited.
    /// </summary>
    void VisitMemberReference(in MemberReferenceContext context, ref TState state);

    /// <summary>
    /// Called when a method implementation is visited.
    /// </summary>
    void VisitMethodImplementation(in MethodImplementationContext context, ref TState state);

    /// <summary>
    /// Called when an exported type is visited.
    /// </summary>
    void VisitExportedType(in ExportedTypeContext context, ref TState state);

    /// <summary>
    /// Called when a manifest resource is visited.
    /// </summary>
    void VisitManifestResource(in ManifestResourceContext context, ref TState state);
}

using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;
using Glean.Providers;
using Glean.Signatures;

namespace Glean.Extensions;

/// <summary>
/// Advanced extensions for decoding event and property signatures.
/// </summary>
/// <remarks>
/// Rich tier: all methods decode signature object graphs and may allocate.
/// For fast tier property/event access use Context properties or DefinitionExtensions instead.
/// </remarks>
public static class AccessorSignatureExtensions
{
    /// <summary>
    /// Gets the event handler type signature for an event.
    /// </summary>
    /// <param name="eventDef">The event definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>The event handler type signature (e.g., EventHandler, EventHandler&lt;TEventArgs&gt;).</returns>
    /// <remarks>
    /// The event handler type is stored in the event's type field in metadata.
    /// This is typically a delegate type like EventHandler or Action&lt;T&gt;.
    /// </remarks>
    public static TypeSignature GetEventHandlerType(this EventDefinition eventDef, MetadataReader reader)
    {
        var typeHandle = eventDef.Type;
        return typeHandle.DecodeTypeSignature(reader, SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
    }

    /// <summary>
    /// Gets the event handler type signature using the event context.
    /// </summary>
    /// <param name="context">The event context.</param>
    /// <returns>The event handler type signature.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeSignature GetEventHandlerType(this EventContext context)
    {
        return context.Definition.GetEventHandlerType(context.Reader);
    }

    /// <summary>
    /// Gets the property type signature for a property.
    /// </summary>
    /// <param name="propertyDef">The property definition.</param>
    /// <returns>The property type signature (the type of the property value).</returns>
    /// <remarks>
    /// This decodes the property signature to extract the return type.
    /// For indexed properties (this[int index]), this returns the element type.
    /// </remarks>
    public static TypeSignature GetPropertyType(this PropertyDefinition propertyDef)
    {
        var signature = propertyDef.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
        return signature.ReturnType;
    }

    /// <summary>
    /// Gets the property type signature using the property context.
    /// </summary>
    /// <param name="context">The property context.</param>
    /// <returns>The property type signature.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeSignature GetPropertyType(this PropertyContext context)
    {
        return context.Definition.GetPropertyType();
    }

    /// <summary>
    /// Checks if a property is an indexer (has parameters).
    /// </summary>
    /// <param name="propertyDef">The property definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the property has index parameters; false if it's a simple property.</returns>
    /// <remarks>
    /// Indexers in C# are properties with parameters, like this[int index].
    /// Simple properties like { get; set; } have no parameters.
    /// Uses <see cref="SignatureInspector"/> to read only the parameter count from the blob;
    /// no signature object graph is constructed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIndexer(this PropertyDefinition propertyDef, MetadataReader reader)
    {
        return SignatureInspector.TryGetPropertyParameterCount(reader, propertyDef.Signature, out var count) && (count > 0);
    }

    /// <summary>
    /// Checks if a property is an indexer using the property context.
    /// </summary>
    /// <param name="context">The property context.</param>
    /// <returns>True if the property is an indexer; false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIndexer(this PropertyContext context)
    {
        return context.IsIndexer();
    }

    /// <summary>
    /// Gets the number of parameters for an indexed property.
    /// </summary>
    /// <param name="propertyDef">The property definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>The number of index parameters (0 for simple properties).</returns>
    /// <remarks>
    /// Uses <see cref="SignatureInspector"/> to read only the parameter count from the blob;
    /// no signature object graph is constructed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIndexParameterCount(this PropertyDefinition propertyDef, MetadataReader reader)
    {
        return SignatureInspector.TryGetPropertyParameterCount(reader, propertyDef.Signature, out var count) ? count : 0;
    }
}

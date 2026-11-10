using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;
using Glean.Internal;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for EventDefinition.
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct EventContext : IEquatable<EventContext>
{
    private readonly MetadataReader _reader;
    private readonly EventDefinitionHandle _handle;
    private readonly EventDefinition _definition;

    /// <summary>
    /// Gets the declaring type context for this event.
    /// </summary>
    public TypeContext DeclaringType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TypeContext.Create(_reader, _definition.GetDeclaringType());
    }

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventContext Create(MetadataReader reader, EventDefinitionHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil)   { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new EventContext(reader, handle, reader.GetEventDefinition(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EventContext UnsafeCreate(MetadataReader reader, EventDefinitionHandle handle)
        => new EventContext(reader, handle, reader.GetEventDefinition(handle));

    private EventContext(MetadataReader reader, EventDefinitionHandle handle, EventDefinition definition)
    {
        _reader = reader;
        _handle = handle;
        _definition = definition;
    }

    /// <summary>Gets the row handle within this context's reader.</summary>
    public EventDefinitionHandle Handle => _handle;
    /// <summary>Gets the underlying metadata definition.</summary>
    public EventDefinition Definition => _definition;
    /// <summary>Gets the borrowed reader that contains this context's metadata.</summary>
    public MetadataReader Reader => _reader;

    /// <summary>Checks for a reader and a row handle that is not nil.</summary>
    /// <remarks>This does not validate the metadata or check whether its owner has been disposed.</remarks>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Gets the event name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _definition.Name;

    /// <summary>Gets the metadata flags for this row.</summary>
    public EventAttributes Attributes => _definition.Attributes;

    /// <summary>
    /// Gets the event type handle.
    /// </summary>
    /// <remarks>
    /// This is the type of the delegate associated with the event.
    /// Can be TypeDefinitionHandle, TypeReferenceHandle, or TypeSpecificationHandle.
    /// </remarks>
    public EntityHandle EventType => _definition.Type;

    /// <summary>Gets the add accessor, or default if absent.</summary>
    public MethodContext AddMethod => GetAccessorContext(_definition.GetAccessors().Adder);

    /// <summary>Gets the remove accessor, or default if absent.</summary>
    public MethodContext RemoveMethod => GetAccessorContext(_definition.GetAccessors().Remover);

    /// <summary>Gets the raise accessor, or default if absent.</summary>
    public MethodContext RaiseMethod => GetAccessorContext(_definition.GetAccessors().Raiser);

    private MethodContext GetAccessorContext(MethodDefinitionHandle handle)
        => handle.IsNil ? default : MethodContext.Create(_reader, handle);

    /// <summary>
    /// True if this event is marked with <c>[CompilerGenerated]</c>.
    /// </summary>
    public bool IsCompilerGenerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => HasAttribute(WellKnownTypes.SystemRuntimeCompilerServicesNs, WellKnownTypes.CompilerGeneratedAttribute);
    }

    /// <summary>
    /// Enumerates this event's custom attributes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeEnumerator EnumerateCustomAttributes()
    {
        return CustomAttributeEnumerator.Create(_reader, _definition.GetCustomAttributes());
    }

    /// <summary>
    /// Enumerates only attributes whose type matches the specified namespace and name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerators.FilteredCustomAttributeEnumerator EnumerateAttributes(string ns, string name)
    {
        return Enumerators.FilteredCustomAttributeEnumerator.Create(_reader, _definition.GetCustomAttributes(), ns, name);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>Finds a custom attribute by namespace and name; returns the context if found.</summary>
    /// <remarks>Zero allocation.</remarks>
    public bool TryFindAttribute(string ns, string name, out CustomAttributeContext attribute)
    {
        var attributes = _definition.GetCustomAttributes();
        if (!attributes.TryFindAttributeHandle(_reader, ns, name, out var handle))
        {
            attribute = default;
            return false;
        }

        attribute = CustomAttributeContext.Create(_reader, handle);
        return true;
    }

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(EventContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is EventContext other) && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(EventContext left, EventContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(EventContext left, EventContext right)
    {
        return !left.Equals(right);
    }
}

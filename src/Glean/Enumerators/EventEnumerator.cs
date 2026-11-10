using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for events.
/// </summary>
public struct EventEnumerator : IStructEnumerator<EventContext>
{
    private readonly MetadataReader _reader;
    private EventDefinitionHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EventEnumerator Create(MetadataReader reader, EventDefinitionHandleCollection collection)
    {
        return new EventEnumerator(reader, collection);
    }

    private EventEnumerator(MetadataReader reader, EventDefinitionHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <inheritdoc/>
    public EventContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => EventContext.Create(_reader, _enumerator.Current);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventEnumerator GetEnumerator()
    {
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

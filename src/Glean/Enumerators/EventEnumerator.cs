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

    public EventContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => EventContext.Create(_reader, _enumerator.Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EventEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for properties.
/// </summary>
public struct PropertyEnumerator : IStructEnumerator<Contexts.PropertyContext>
{
    private readonly MetadataReader _reader;
    private PropertyDefinitionHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PropertyEnumerator Create(MetadataReader reader, PropertyDefinitionHandleCollection collection)
    {
        return new PropertyEnumerator(reader, collection);
    }

    private PropertyEnumerator(MetadataReader reader, PropertyDefinitionHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <inheritdoc/>
    public PropertyContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => PropertyContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PropertyEnumerator GetEnumerator()
    {
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

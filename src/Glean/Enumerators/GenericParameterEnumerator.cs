using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for generic parameters.
/// </summary>
public struct GenericParameterEnumerator : IStructEnumerator<Contexts.GenericParameterContext>
{
    private readonly MetadataReader _reader;
    private GenericParameterHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GenericParameterEnumerator Create(MetadataReader reader, GenericParameterHandleCollection collection)
    {
        return new GenericParameterEnumerator(reader, collection);
    }

    private GenericParameterEnumerator(MetadataReader reader, GenericParameterHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <inheritdoc/>
    public GenericParameterContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GenericParameterContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GenericParameterEnumerator GetEnumerator()
    {
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

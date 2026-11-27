using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>Enumerates generic parameter constraint contexts without decoding signatures or allocating.</summary>
public struct GenericParameterConstraintEnumerator : IStructEnumerator<GenericParameterConstraintContext>
{
    private readonly MetadataReader _reader;
    private GenericParameterConstraintHandleCollection.Enumerator _enumerator;

    internal GenericParameterConstraintEnumerator(MetadataReader reader, GenericParameterConstraintHandleCollection collection)
    {
        _reader = reader;
        _enumerator = collection.GetEnumerator();
    }

    /// <inheritdoc/>
    public GenericParameterConstraintContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GenericParameterConstraintContext.Create(_reader, _enumerator.Current);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    public GenericParameterConstraintEnumerator GetEnumerator() => this;

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

using System.ComponentModel;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for portable PDB sequence points.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public struct SequencePointEnumerator : IStructEnumerator<SequencePoint>
{
    private SequencePointCollection.Enumerator _inner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SequencePointEnumerator Create(SequencePointCollection collection)
    {
        return new SequencePointEnumerator(collection.GetEnumerator());
    }

    private SequencePointEnumerator(SequencePointCollection.Enumerator inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    public SequencePoint Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Current;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _inner.MoveNext();
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SequencePointEnumerator GetEnumerator()
    {
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

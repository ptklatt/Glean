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

    public SequencePoint Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _inner.Current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _inner.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SequencePointEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

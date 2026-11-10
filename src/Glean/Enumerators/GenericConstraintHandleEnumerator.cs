using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Enumerators;

/// <summary>
/// Fast tier struct enumerator for generic constraint type handles.
/// This does not decode signatures and does not allocate during foreach iteration.
/// </summary>
public struct GenericConstraintHandleEnumerator : IStructEnumerator<System.Reflection.Metadata.EntityHandle>
{
    private readonly MetadataReader _reader;
    private GenericParameterConstraintHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GenericConstraintHandleEnumerator Create(
        MetadataReader reader,
        GenericParameterConstraintHandleCollection collection)
    {
        return new GenericConstraintHandleEnumerator(reader, collection);
    }

    private GenericConstraintHandleEnumerator(
        MetadataReader reader,
        GenericParameterConstraintHandleCollection collection)
    {
        _reader = reader;
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current constraint type handle.
    /// </summary>
    public EntityHandle Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetGenericParameterConstraint(_enumerator.Current).Type;
    }

    /// <summary>
    /// Gets the current GenericParamConstraint row handle.
    /// </summary>
    public GenericParameterConstraintHandle ConstraintHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _enumerator.Current;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GenericConstraintHandleEnumerator GetEnumerator() => this;

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

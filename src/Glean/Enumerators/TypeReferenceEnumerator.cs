using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for type references.
/// </summary>
public struct TypeReferenceEnumerator : IStructEnumerator<Contexts.TypeReferenceContext>
{
    private readonly MetadataReader _reader;
    private TypeReferenceHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TypeReferenceEnumerator Create(MetadataReader reader, TypeReferenceHandleCollection collection)
    {
        return new TypeReferenceEnumerator(reader, collection);
    }

    private TypeReferenceEnumerator(MetadataReader reader, TypeReferenceHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current type reference context.
    /// </summary>
    public TypeReferenceContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TypeReferenceContext.Create(_reader, _enumerator.Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeReferenceEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

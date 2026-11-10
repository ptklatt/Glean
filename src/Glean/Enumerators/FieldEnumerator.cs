using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for fields.
/// </summary>
public struct FieldEnumerator : IStructEnumerator<Contexts.FieldContext>
{
    private readonly MetadataReader _reader;
    private FieldDefinitionHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldEnumerator Create(MetadataReader reader, FieldDefinitionHandleCollection collection)
    {
        return new FieldEnumerator(reader, collection);
    }

    private FieldEnumerator(MetadataReader reader, FieldDefinitionHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <inheritdoc/>
    public FieldContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FieldContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FieldEnumerator GetEnumerator()
    {
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

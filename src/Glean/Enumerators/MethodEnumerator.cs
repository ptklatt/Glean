using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for methods.
/// </summary>
public struct MethodEnumerator : IStructEnumerator<Contexts.MethodContext>
{
    private readonly MetadataReader _reader;
    private MethodDefinitionHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodEnumerator Create(MetadataReader reader, MethodDefinitionHandleCollection collection)
    {
        return new MethodEnumerator(reader, collection);
    }

    private MethodEnumerator(MetadataReader reader, MethodDefinitionHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current method context.
    /// </summary>
    public MethodContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MethodContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

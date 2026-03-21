using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for method implementations (explicit interface implementations).
/// </summary>
public struct MethodImplementationEnumerator : IStructEnumerator<Contexts.MethodImplementationContext>
{
    private readonly MetadataReader _reader;
    private MethodImplementationHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MethodImplementationEnumerator Create(MetadataReader reader, MethodImplementationHandleCollection collection)
    {
        return new MethodImplementationEnumerator(reader, collection);
    }

    private MethodImplementationEnumerator(MetadataReader reader, MethodImplementationHandleCollection collection)
    {
        _reader = reader;
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current method implementation context.
    /// </summary>
    public MethodImplementationContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var handle = _enumerator.Current;
            return MethodImplementationContext.Create(_reader, handle);
        }
    }

    /// <summary>
    /// Moves to the next method implementation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodImplementationEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}

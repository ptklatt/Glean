using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for assembly references.
/// </summary>
public struct AssemblyReferenceEnumerator : IStructEnumerator<Contexts.AssemblyReferenceContext>
{
    private readonly MetadataReader _reader;
    private AssemblyReferenceHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static AssemblyReferenceEnumerator Create(MetadataReader reader, AssemblyReferenceHandleCollection collection)
    {
        return new AssemblyReferenceEnumerator(reader, collection);
    }

    private AssemblyReferenceEnumerator(MetadataReader reader, AssemblyReferenceHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current assembly reference context.
    /// </summary>
    public AssemblyReferenceContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AssemblyReferenceContext.Create(_reader, _enumerator.Current);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssemblyReferenceEnumerator GetEnumerator()
    {
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

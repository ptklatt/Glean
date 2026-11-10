using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Enumerators;

/// <summary>
/// Fast tier struct enumerator for implemented interface type handles.
/// This does not decode signatures and does not allocate during foreach iteration.
/// </summary>
public struct InterfaceTypeHandleEnumerator : IStructEnumerator<System.Reflection.Metadata.EntityHandle>
{
    private readonly MetadataReader _reader;
    private InterfaceImplementationHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InterfaceTypeHandleEnumerator Create(
        MetadataReader reader,
        InterfaceImplementationHandleCollection collection)
    {
        return new InterfaceTypeHandleEnumerator(reader, collection);
    }

    private InterfaceTypeHandleEnumerator(
        MetadataReader reader,
        InterfaceImplementationHandleCollection collection)
    {
        _reader = reader;
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current implemented interface type handle.
    /// </summary>
    public EntityHandle Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetInterfaceImplementation(_enumerator.Current).Interface;
    }

    /// <summary>
    /// Gets the current InterfaceImpl row handle.
    /// </summary>
    public InterfaceImplementationHandle InterfaceImplementationHandle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _enumerator.Current;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InterfaceTypeHandleEnumerator GetEnumerator() => this;

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

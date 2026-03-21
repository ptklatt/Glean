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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InterfaceTypeHandleEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}

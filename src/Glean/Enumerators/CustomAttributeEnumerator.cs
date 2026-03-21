using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero-allocation enumerator for custom attributes.
/// </summary>
public struct CustomAttributeEnumerator : IStructEnumerator<CustomAttributeContext>
{
    private readonly MetadataReader _reader;
    private CustomAttributeHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CustomAttributeEnumerator Create(MetadataReader reader, CustomAttributeHandleCollection collection)
    {
        return new CustomAttributeEnumerator(reader, collection);
    }

    private CustomAttributeEnumerator(MetadataReader reader, CustomAttributeHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    public CustomAttributeContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CustomAttributeContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

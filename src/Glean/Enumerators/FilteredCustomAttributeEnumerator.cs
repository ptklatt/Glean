using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;
using Glean.Extensions;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator that yields only custom attributes matching a given namespace and name.
/// </summary>
public struct FilteredCustomAttributeEnumerator : IStructEnumerator<CustomAttributeContext>
{
    private readonly MetadataReader _reader;
    private readonly string _ns;
    private readonly string _name;
    private CustomAttributeHandleCollection.Enumerator _enumerator;
    private CustomAttributeContext _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FilteredCustomAttributeEnumerator Create(
        MetadataReader reader,
        CustomAttributeHandleCollection collection,
        string ns,
        string name)
    {
        return new FilteredCustomAttributeEnumerator(reader, collection, ns, name);
    }

    private FilteredCustomAttributeEnumerator(
        MetadataReader reader,
        CustomAttributeHandleCollection collection,
        string ns,
        string name)
    {
        _reader = reader;
        _ns = ns;
        _name = name;
        _enumerator = collection.GetEnumerator();
        _current = default;
    }

    public CustomAttributeContext Current => _current;

    public bool MoveNext()
    {
        while (_enumerator.MoveNext())
        {
            var handle = _enumerator.Current;
            var attribute = _reader.GetCustomAttribute(handle);
            if (attribute.IsAttributeType(_reader, _ns, _name))
            {
                _current = CustomAttributeContext.UnsafeCreate(_reader, handle);
                return true;
            }
        }

        _current = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredCustomAttributeEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

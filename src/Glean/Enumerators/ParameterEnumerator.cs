using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for parameters.
/// </summary>
public struct ParameterEnumerator : IStructEnumerator<Contexts.ParameterContext>
{
    private readonly MetadataReader _reader;
    private readonly MethodDefinitionHandle _declaringMethod;
    private ParameterHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ParameterEnumerator Create(MetadataReader reader, ParameterHandleCollection collection, MethodDefinitionHandle declaringMethod = default)
    {
        return new ParameterEnumerator(reader, collection, declaringMethod);
    }

    private ParameterEnumerator(MetadataReader reader, ParameterHandleCollection collection, MethodDefinitionHandle declaringMethod)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _declaringMethod = declaringMethod;
        _enumerator = collection.GetEnumerator();
    }

    public ParameterContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ParameterContext.UnsafeCreate(_reader, _enumerator.Current, _declaringMethod);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        return _enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ParameterEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

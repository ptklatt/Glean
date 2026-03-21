using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for all type definitions in an assembly.
/// </summary>
public struct TypeDefinitionEnumerator : IStructEnumerator<Contexts.TypeContext>
{
    private readonly MetadataReader _reader;
    private TypeDefinitionHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TypeDefinitionEnumerator Create(MetadataReader reader, TypeDefinitionHandleCollection collection)
    {
        return new TypeDefinitionEnumerator(reader, collection);
    }

    private TypeDefinitionEnumerator(MetadataReader reader, TypeDefinitionHandleCollection collection)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumerator = collection.GetEnumerator();
    }

    /// <summary>
    /// Gets the current type context.
    /// </summary>
    public TypeContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TypeContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    /// <summary>
    /// Advances the enumerator to the next type definition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeDefinitionEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for nested types.
/// </summary>
public struct NestedTypeEnumerator : IStructEnumerator<Contexts.TypeContext>
{
    private readonly MetadataReader _reader;
    private ImmutableArray<TypeDefinitionHandle>.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NestedTypeEnumerator Create(MetadataReader reader, ImmutableArray<TypeDefinitionHandle> nestedTypes)
    {
        return new NestedTypeEnumerator(reader, nestedTypes);
    }

    private NestedTypeEnumerator(MetadataReader reader, ImmutableArray<TypeDefinitionHandle> nestedTypes)
    {
        _reader = reader;
        _enumerator = nestedTypes.GetEnumerator();
    }

    /// <summary>
    /// Gets the current nested type context.
    /// </summary>
    public TypeContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TypeContext.UnsafeCreate(_reader, _enumerator.Current);
    }

    /// <summary>
    /// Moves to the next nested type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NestedTypeEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}

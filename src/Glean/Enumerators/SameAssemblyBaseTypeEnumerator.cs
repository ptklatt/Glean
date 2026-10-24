using System.ComponentModel;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for walking the base type chain within one module.
/// </summary>
/// <remarks>
/// This enumerator follows only <see cref="System.Reflection.Metadata.TypeDefinitionHandle"/> base types.
/// It stops when the next base type is external, encoded as a type specification, or missing.
/// Cyclic metadata terminates the walk without allocating a visited set.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public struct SameAssemblyBaseTypeEnumerator : IStructEnumerator<TypeContext>
{
    private TypeContext _current;
    private readonly TypeDefinitionHandle _startHandle;
    private TypeDefinitionHandle _cycleCheckpoint;
    private long _cyclePower;
    private long _cycleLength;
    private bool _finished;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SameAssemblyBaseTypeEnumerator Create(TypeContext type)
    {
        return new SameAssemblyBaseTypeEnumerator(type);
    }

    private SameAssemblyBaseTypeEnumerator(TypeContext type)
    {
        _current = type;
        _startHandle = type.Handle;
        _cycleCheckpoint = type.Handle;
        _cyclePower = 1;
        _cycleLength = 0;
        _finished = false;
    }

    public TypeContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    public bool MoveNext()
    {
        if (_finished)
        {
            return false;
        }

        if (!_current.TryGetBaseTypeDefinition(out var baseType))
        {
            _finished = true;
            return false;
        }

        if ((baseType.Handle == _startHandle) || (baseType.Handle == _cycleCheckpoint))
        {
            _finished = true;
            return false;
        }

        _current = baseType;
        _cycleLength++;
        if (_cycleLength == _cyclePower)
        {
            // Exponentially spaced checkpoints detect cycles without extra metadata reads.
            _cycleCheckpoint = baseType.Handle;
            _cyclePower *= 2;
            _cycleLength = 0;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SameAssemblyBaseTypeEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

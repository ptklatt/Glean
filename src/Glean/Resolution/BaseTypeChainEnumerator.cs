using System.ComponentModel;
using System.Runtime.CompilerServices;

using Glean.Contexts;
using Glean.Enumerators;
using Glean.Extensions;

namespace Glean.Resolution;

/// <summary>
/// Struct enumerator for walking a base type chain across assemblies.
/// </summary>
/// <remarks>
/// Cycle checks do not allocate or perform extra resolution. Resolution may populate assembly caches.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public struct BaseTypeChainEnumerator : IStructEnumerator<TypeContext>
{
    private readonly AssemblySet _assemblySet;
    private readonly TypeContext _startType;
    private TypeContext _current;
    private TypeContext _cycleCheckpoint;
    private long _cyclePower;
    private long _cycleLength;
    private bool _finished;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BaseTypeChainEnumerator Create(TypeContext type, AssemblySet assemblySet)
    {
        return new BaseTypeChainEnumerator(type, assemblySet);
    }

    private BaseTypeChainEnumerator(TypeContext type, AssemblySet assemblySet)
    {
        _current = type;
        _assemblySet = assemblySet ?? throw new ArgumentNullException(nameof(assemblySet));
        _startType = type;
        _cycleCheckpoint = type;
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

        if (!_current.TryGetBaseType(_assemblySet, out var baseType))
        {
            _finished = true;
            return false;
        }

        if (baseType.Equals(_startType) || baseType.Equals(_cycleCheckpoint))
        {
            _finished = true;
            return false;
        }

        _current = baseType;
        _cycleLength++;
        if (_cycleLength == _cyclePower)
        {
            // Exponentially spaced checkpoints detect cycles without extra resolution.
            _cycleCheckpoint = baseType;
            _cyclePower *= 2;
            _cycleLength = 0;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BaseTypeChainEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

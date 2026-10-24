using System.ComponentModel;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;
using Glean.Enumerators;

namespace Glean.Resolution;

/// <summary>
/// Enumerates direct and inherited interface definitions across a base type chain.
/// </summary>
/// <remarks>
/// Resolution builds a queue and visited set as interfaces are discovered. Results are deduplicated
/// by metadata reader and type handle. Generic instances are reported as their type definitions.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public struct InterfaceTypeChainEnumerator : IStructEnumerator<TypeContext>
{
    private readonly AssemblySet _assemblySet;
    private readonly TypeContext _startType;
    private BaseTypeChainEnumerator _baseTypes;
    private TypeContext _activeType;
    private InterfaceImplementationHandleCollection.Enumerator _activeInterfaces;
    private Queue<TypeContext>? _pendingInterfaceTypes;
    private HashSet<TypeContext>? _visitedInterfaces;
    private TypeContext _current;
    private bool _started;
    private bool _finished;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static InterfaceTypeChainEnumerator Create(TypeContext type, AssemblySet assemblySet)
    {
        return new InterfaceTypeChainEnumerator(type, assemblySet);
    }

    private InterfaceTypeChainEnumerator(TypeContext type, AssemblySet assemblySet)
    {
        _assemblySet = assemblySet ?? throw new ArgumentNullException(nameof(assemblySet));
        _startType = type;
        _baseTypes = BaseTypeChainEnumerator.Create(type, assemblySet);
        _activeType = type;
        _activeInterfaces = default;
        _pendingInterfaceTypes = null;
        _visitedInterfaces = null;
        _current = default;
        _started = false;
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

        if (!_started)
        {
            _started = true;
            _activeType = _startType;
            _activeInterfaces = _activeType.Definition.GetInterfaceImplementations().GetEnumerator();
        }

        while (true)
        {
            while (_activeInterfaces.MoveNext())
            {
                var implementation = _activeType.Reader.GetInterfaceImplementation(_activeInterfaces.Current);
                if (ResolutionTypeHelpers.TryResolveTypeHandle(
                    _activeType.Reader,
                    implementation.Interface,
                    _assemblySet,
                    out var resolvedType))
                {
                    if (resolvedType.Equals(_startType))
                    {
                        continue;
                    }

                    _visitedInterfaces ??= new HashSet<TypeContext>();
                    if (!_visitedInterfaces.Add(resolvedType))
                    {
                        continue;
                    }

                    _pendingInterfaceTypes ??= new Queue<TypeContext>();
                    _pendingInterfaceTypes.Enqueue(resolvedType);
                    _current = resolvedType;
                    return true;
                }
            }

            if ((_pendingInterfaceTypes is not null) && (_pendingInterfaceTypes.Count > 0))
            {
                _activeType = _pendingInterfaceTypes.Dequeue();
                _activeInterfaces = _activeType.Definition.GetInterfaceImplementations().GetEnumerator();
                continue;
            }

            if (!_baseTypes.MoveNext())
            {
                _finished = true;
                return false;
            }

            _activeType = _baseTypes.Current;
            _activeInterfaces = _activeType.Definition.GetInterfaceImplementations().GetEnumerator();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InterfaceTypeChainEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

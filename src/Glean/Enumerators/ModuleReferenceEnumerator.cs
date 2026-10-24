using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for module references.
/// </summary>
public struct ModuleReferenceEnumerator : IStructEnumerator<ModuleReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly int _rowCount;
    private int _nextRowNumber;
    private ModuleReferenceHandle _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ModuleReferenceEnumerator Create(MetadataReader reader)
    {
        return new ModuleReferenceEnumerator(reader);
    }

    private ModuleReferenceEnumerator(MetadataReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _rowCount = reader.GetTableRowCount(TableIndex.ModuleRef);
        _nextRowNumber = 1;
        _current = default;
    }

    /// <summary>
    /// Gets the current module reference context.
    /// </summary>
    public ModuleReferenceContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ModuleReferenceContext.Create(_reader, _current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_nextRowNumber > _rowCount)
        {
            return false;
        }

        _current = MetadataTokens.ModuleReferenceHandle(_nextRowNumber);
        _nextRowNumber++;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ModuleReferenceEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }
}

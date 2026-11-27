using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>Enumerates method instantiation rows without decoding signatures or allocating.</summary>
public struct MethodSpecificationEnumerator : IStructEnumerator<MethodSpecificationContext>
{
    private readonly MetadataReader _reader;
    private readonly int _rowCount;
    private int _rowNumber;

    internal MethodSpecificationEnumerator(MetadataReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _rowCount = reader.GetTableRowCount(TableIndex.MethodSpec);
        _rowNumber = 0;
    }

    /// <inheritdoc/>
    public MethodSpecificationContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MethodSpecificationContext.Create(_reader, MetadataTokens.MethodSpecificationHandle(_rowNumber));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_rowNumber >= _rowCount)
        {
            return false;
        }

        _rowNumber++;
        return true;
    }

    /// <summary>Returns a copy of this enumerator at its current position.</summary>
    public MethodSpecificationEnumerator GetEnumerator() => this;

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}

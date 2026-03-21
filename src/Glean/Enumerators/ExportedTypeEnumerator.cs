using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for exported types.
/// </summary>
public struct ExportedTypeEnumerator : IStructEnumerator<ExportedTypeContext>
{
    private readonly MetadataReader _reader;
    private ExportedTypeHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ExportedTypeEnumerator Create(MetadataReader reader)
    {
        return new ExportedTypeEnumerator(reader);
    }

    private ExportedTypeEnumerator(MetadataReader reader)
    {
        _reader = reader;
        _enumerator = reader.ExportedTypes.GetEnumerator();
    }

    /// <summary>
    /// Gets the current exported type context.
    /// </summary>
    public ExportedTypeContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var handle = _enumerator.Current;
            return ExportedTypeContext.Create(_reader, handle);
        }
    }

    /// <summary>
    /// Moves to the next exported type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ExportedTypeEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}

using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;

namespace Glean.Enumerators;

/// <summary>
/// Zero allocation enumerator for manifest resources.
/// </summary>
public struct ManifestResourceEnumerator : IStructEnumerator<ManifestResourceContext>
{
    private readonly MetadataReader _reader;
    private ManifestResourceHandleCollection.Enumerator _enumerator;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ManifestResourceEnumerator Create(MetadataReader reader)
    {
        return new ManifestResourceEnumerator(reader);
    }

    private ManifestResourceEnumerator(MetadataReader reader)
    {
        _reader = reader;
        _enumerator = reader.ManifestResources.GetEnumerator();
    }

    /// <summary>
    /// Gets the current manifest resource context.
    /// </summary>
    public ManifestResourceContext Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var handle = _enumerator.Current;
            return ManifestResourceContext.Create(_reader, handle);
        }
    }

    /// <summary>
    /// Moves to the next manifest resource.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() => _enumerator.MoveNext();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ManifestResourceEnumerator GetEnumerator() => this;

    public void Dispose()
    {
    }
}

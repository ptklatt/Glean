using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for ManifestResource.
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct ManifestResourceContext : IEquatable<ManifestResourceContext>
{
    private readonly MetadataReader _reader;
    private readonly ManifestResourceHandle _handle;
    private readonly ManifestResource _resource;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ManifestResourceContext Create(MetadataReader reader, ManifestResourceHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new ManifestResourceContext(reader, handle, reader.GetManifestResource(handle));
    }

    private ManifestResourceContext(MetadataReader reader, ManifestResourceHandle handle, ManifestResource resource)
    {
        _reader = reader;
        _handle = handle;
        _resource = resource;
    }

    /// <summary>Gets the row handle within this context's reader.</summary>
    public ManifestResourceHandle Handle => _handle;
    /// <summary>Gets the underlying manifest resource row.</summary>
    public ManifestResource Resource => _resource;
    /// <summary>Gets the borrowed reader that contains this context's metadata.</summary>
    public MetadataReader Reader => _reader;

    /// <summary>Checks for a reader and a row handle that is not nil.</summary>
    /// <remarks>This does not validate the metadata or check whether its owner has been disposed.</remarks>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the resource name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_resource.Name);
    }

    /// <summary>
    /// Gets the name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _resource.Name;

    /// <summary>
    /// Gets the resource attributes.
    /// </summary>
    public ManifestResourceAttributes Attributes => _resource.Attributes;

    /// <summary>
    /// Gets the resource offset.
    /// </summary>
    public long Offset => _resource.Offset;

    /// <summary>
    /// Gets the implementation entity (assembly reference for linked resources, nil for embedded).
    /// </summary>
    public EntityHandle Implementation => _resource.Implementation;

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ManifestResourceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is ManifestResourceContext other) && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(ManifestResourceContext left, ManifestResourceContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(ManifestResourceContext left, ManifestResourceContext right)
    {
        return !left.Equals(right);
    }
}

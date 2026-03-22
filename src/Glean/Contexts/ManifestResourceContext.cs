using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for ManifestResource.
/// </summary>
public readonly struct ManifestResourceContext : IEquatable<ManifestResourceContext>
{
    private readonly MetadataReader _reader;
    private readonly ManifestResourceHandle _handle;
    private readonly ManifestResource _resource;

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

    public ManifestResourceHandle Handle => _handle;
    public ManifestResource Resource => _resource;
    public MetadataReader Reader => _reader;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ManifestResourceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is ManifestResourceContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(ManifestResourceContext left, ManifestResourceContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ManifestResourceContext left, ManifestResourceContext right)
    {
        return !left.Equals(right);
    }
}

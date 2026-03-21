using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for AssemblyReference.
/// </summary>
public readonly struct AssemblyReferenceContext : IEquatable<AssemblyReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly AssemblyReferenceHandle _handle;
    private readonly AssemblyReference _reference;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssemblyReferenceContext Create(MetadataReader reader, AssemblyReferenceHandle handle)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil)  { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new AssemblyReferenceContext(reader, handle, reader.GetAssemblyReference(handle));
    }

    private AssemblyReferenceContext(MetadataReader reader, AssemblyReferenceHandle handle, AssemblyReference reference)
    {
        _reader = reader;
        _handle = handle;
        _reference = reference;
    }

    public AssemblyReferenceHandle Handle => _handle;
    public AssemblyReference Reference => _reference;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the assembly reference name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_reference.Name);
    }

    /// <summary>
    /// Gets the assembly version.
    /// </summary>
    public Version Version => _reference.Version;

    /// <summary>
    /// Gets the assembly culture.
    /// </summary>
    public string Culture
    {
        get
        {
            var cultureHandle = _reference.Culture;
            return cultureHandle.IsNil ? string.Empty : _reader.GetString(cultureHandle);
        }
    }

    /// <summary>
    /// Gets the public key or token blob handle.
    /// </summary>
    public BlobHandle PublicKeyOrToken => _reference.PublicKeyOrToken;

    /// <summary>
    /// Gets the assembly flags.
    /// </summary>
    public AssemblyFlags Flags => _reference.Flags;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssemblyReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is AssemblyReferenceContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(AssemblyReferenceContext left, AssemblyReferenceContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AssemblyReferenceContext left, AssemblyReferenceContext right)
    {
        return !left.Equals(right);
    }
}

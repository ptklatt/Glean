using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for AssemblyReference.
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct AssemblyReferenceContext : IEquatable<AssemblyReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly AssemblyReferenceHandle _handle;
    private readonly AssemblyReference _reference;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
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

    /// <summary>Gets the row handle within this context's reader.</summary>
    public AssemblyReferenceHandle Handle => _handle;
    /// <summary>Gets the underlying metadata reference row.</summary>
    public AssemblyReference Reference => _reference;
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

    /// <summary>
    /// Enumerates this assembly reference's custom attributes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeEnumerator EnumerateCustomAttributes()
    {
        return CustomAttributeEnumerator.Create(_reader, _reference.GetCustomAttributes());
    }
    
    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AssemblyReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is AssemblyReferenceContext other) && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(AssemblyReferenceContext left, AssemblyReferenceContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(AssemblyReferenceContext left, AssemblyReferenceContext right)
    {
        return !left.Equals(right);
    }
}

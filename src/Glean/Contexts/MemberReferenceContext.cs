using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for MemberReference.
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct MemberReferenceContext : IEquatable<MemberReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly MemberReferenceHandle _handle;
    private readonly MemberReference _reference;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemberReferenceContext Create(MetadataReader reader, MemberReferenceHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new MemberReferenceContext(reader, handle, reader.GetMemberReference(handle));
    }

    private MemberReferenceContext(MetadataReader reader, MemberReferenceHandle handle, MemberReference reference)
    {
        _reader = reader;
        _handle = handle;
        _reference = reference;
    }

    /// <summary>Gets the row handle within this context's reader.</summary>
    public MemberReferenceHandle Handle => _handle;
    /// <summary>Gets the underlying metadata reference row.</summary>
    public MemberReference Reference => _reference;
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
    /// Gets the member reference name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_reference.Name);
    }

    /// <summary>
    /// Checks whether the member reference name matches the specified string (zero allocation).
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle-based <c>MetadataStringComparer</c>.
    /// Prefer this over <c>Name == name</c>, which allocates a string.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_reference.Name, name);
    }

    /// <summary>
    /// Gets the parent handle (TypeDefinitionHandle, TypeReferenceHandle, or TypeSpecificationHandle).
    /// </summary>
    public EntityHandle Parent => _reference.Parent;

    /// <summary>
    /// Gets the member kind (Method or Field).
    /// </summary>
    public MemberReferenceKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reference.GetKind();
    }

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MemberReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is MemberReferenceContext other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(MemberReferenceContext left, MemberReferenceContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(MemberReferenceContext left, MemberReferenceContext right)
    {
        return !left.Equals(right);
    }
}

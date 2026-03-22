using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for MemberReference.
/// </summary>
public readonly struct MemberReferenceContext : IEquatable<MemberReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly MemberReferenceHandle _handle;
    private readonly MemberReference _reference;

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

    public MemberReferenceHandle Handle => _handle;
    public MemberReference Reference => _reference;
    public MetadataReader Reader => _reader;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MemberReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is MemberReferenceContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(MemberReferenceContext left, MemberReferenceContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MemberReferenceContext left, MemberReferenceContext right)
    {
        return !left.Equals(right);
    }
}

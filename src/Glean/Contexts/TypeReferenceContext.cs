using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for TypeReference.
/// </summary>
public readonly struct TypeReferenceContext : IEquatable<TypeReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly TypeReferenceHandle _handle;
    private readonly TypeReference _reference;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeReferenceContext Create(MetadataReader reader, TypeReferenceHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new TypeReferenceContext(reader, handle, reader.GetTypeReference(handle));
    }

    private TypeReferenceContext(MetadataReader reader, TypeReferenceHandle handle, TypeReference reference)
    {
        _reader = reader;
        _handle = handle;
        _reference = reference;
    }

    public TypeReferenceHandle Handle => _handle;
    public TypeReference Reference => _reference;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the type reference name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_reference.Name);
    }

    /// <summary>
    /// Gets the name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _reference.Name;

    /// <summary>
    /// Gets the namespace.
    /// </summary>
    public string Namespace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_reference.Namespace);
    }

    /// <summary>
    /// Gets the namespace handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NamespaceHandle => _reference.Namespace;

    /// <summary>
    /// Gets the full type name (Namespace.Name).
    /// </summary>
    /// <remarks>
    /// Allocates: 2-3 strings (namespace + name + concatenation).
    /// </remarks>
    public string FullName
    {
        get
        {
            var ns = Namespace;
            var name = Name;
            return string.IsNullOrEmpty(ns) ? name : string.Concat(ns, ".", name);
        }
    }

    /// <summary>
    /// Checks whether the type reference name matches the specified name handle.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer the extension method on <see cref="Reference"/> to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(StringHandle nameHandle)
    {
        return _reference.Name == nameHandle;
    }

    /// <summary>
    /// Checks whether the type reference name matches the specified string (zero allocation).
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle based <c>MetadataStringComparer</c>.
    /// Prefer this over <c>Name == name</c>, which allocates a string.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_reference.Name, name);
    }

    /// <summary>
    /// Checks whether the namespace matches the specified namespace handle.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer the extension method on <see cref="Reference"/> to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NamespaceIs(StringHandle namespaceHandle)
    {
        return _reference.Namespace == namespaceHandle;
    }

    /// <summary>
    /// Checks whether the namespace matches the specified string (zero allocation).
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle based <c>MetadataStringComparer</c>.
    /// Prefer this over <c>Namespace == ns</c>, which allocates a string.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NamespaceIs(string ns)
    {
        return _reader.StringComparer.Equals(_reference.Namespace, ns);
    }

    /// <summary>
    /// Checks whether namespace and name match the specified handles.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer the extension method on <see cref="Reference"/> to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Is(StringHandle namespaceHandle, StringHandle nameHandle)
    {
        return (_reference.Namespace == namespaceHandle) && (_reference.Name == nameHandle);
    }

    /// <summary>
    /// Checks whether namespace and name match the specified strings (zero allocation).
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle based <c>MetadataStringComparer</c>.
    /// Prefer this over comparing <c>Namespace</c> and <c>Name</c> separately, which allocates strings.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Is(string ns, string name)
    {
        return _reader.StringComparer.Equals(_reference.Namespace, ns) &&
               _reader.StringComparer.Equals(_reference.Name, name);
    }

    /// <summary>
    /// Gets the resolution scope (assembly or module where the type is defined).
    /// </summary>
    public EntityHandle ResolutionScope => _reference.ResolutionScope;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TypeReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is TypeReferenceContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(TypeReferenceContext left, TypeReferenceContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TypeReferenceContext left, TypeReferenceContext right)
    {
        return !left.Equals(right);
    }
}

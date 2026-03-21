using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for ExportedType.
/// </summary>
public readonly struct ExportedTypeContext : IEquatable<ExportedTypeContext>
{
    private readonly MetadataReader _reader;
    private readonly ExportedTypeHandle _handle;
    private readonly ExportedType _exportedType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ExportedTypeContext Create(MetadataReader reader, ExportedTypeHandle handle)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new ExportedTypeContext(reader, handle, reader.GetExportedType(handle));
    }

    private ExportedTypeContext(MetadataReader reader, ExportedTypeHandle handle, ExportedType exportedType)
    {
        _reader = reader;
        _handle = handle;
        _exportedType = exportedType;
    }

    public ExportedTypeHandle Handle => _handle;
    public ExportedType ExportedType => _exportedType;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the exported type name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_exportedType.Name);
    }

    /// <summary>
    /// Gets the name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _exportedType.Name;

    /// <summary>
    /// Gets the namespace.
    /// </summary>
    public string Namespace
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_exportedType.Namespace);
    }

    /// <summary>
    /// Gets the namespace handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NamespaceHandle => _exportedType.Namespace;

    /// <summary>
    /// Gets the implementation entity (assembly reference or exported type for nested types).
    /// </summary>
    public EntityHandle Implementation => _exportedType.Implementation;

    /// <summary>
    /// Gets the type attributes.
    /// </summary>
    public TypeAttributes Attributes => _exportedType.Attributes;

    /// <summary>
    /// Gets whether this is a type forwarder.
    /// </summary>
    public bool IsForwarder => _exportedType.IsForwarder;

    /// <summary>
    /// Checks whether the type name matches the specified name handle.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer the extension method on <see cref="ExportedType"/> to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(StringHandle nameHandle)
    {
        return _exportedType.Name == nameHandle;
    }

    /// <summary>
    /// Checks whether the type name matches the specified string (zero allocation).
    /// </summary>
    /// <remarks>
    /// Zero allocation: uses the reader's handle based <c>MetadataStringComparer</c>.
    /// Prefer this over <c>Name == name</c>, which allocates a string.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_exportedType.Name, name);
    }

    /// <summary>
    /// Checks whether the namespace matches the specified namespace handle.
    /// </summary>
    /// <remarks>For handle level comparison at the System.Reflection.Metadata layer,
    /// prefer the extension method on <see cref="ExportedType"/> to keep the two layers distinct.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NamespaceIs(StringHandle namespaceHandle)
    {
        return _exportedType.Namespace == namespaceHandle;
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
        return _reader.StringComparer.Equals(_exportedType.Namespace, ns);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ExportedTypeContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is ExportedTypeContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(ExportedTypeContext left, ExportedTypeContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ExportedTypeContext left, ExportedTypeContext right)
    {
        return !left.Equals(right);
    }
}

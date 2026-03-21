using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a type reference signature to an external type.
/// Zero allocation wrapper around (MetadataReader, TypeReferenceHandle).
/// </summary>
public sealed class TypeReferenceSignature : TypeSignature
{
    private readonly MetadataReader _reader;
    private readonly TypeReferenceHandle _handle;
    private readonly EntityHandle _resolutionScope;
    private string? _resolutionScopeName;

    /// <summary>
    /// Gets the metadata reader.
    /// </summary>
    public MetadataReader Reader => _reader;

    /// <summary>
    /// Gets the type reference handle.
    /// </summary>
    public TypeReferenceHandle Handle => _handle;

    /// <summary>
    /// Gets the resolution scope handle (AssemblyReference, ModuleReference, etc.).
    /// </summary>
    public EntityHandle ResolutionScope => _resolutionScope;

    /// <summary>
    /// Gets the resolution scope name (lazy evaluation).
    /// </summary>
    public string? ResolutionScopeName
    {
        get
        {
            if (_resolutionScopeName != null) { return _resolutionScopeName; }
            if (_resolutionScope.IsNil) { return null; }

            _resolutionScopeName = _resolutionScope.Kind switch
            {
                HandleKind.AssemblyReference => _reader.GetString(_reader.GetAssemblyReference((AssemblyReferenceHandle)_resolutionScope).Name),
                HandleKind.ModuleReference   => _reader.GetString(_reader.GetModuleReference((ModuleReferenceHandle)_resolutionScope).Name),
                HandleKind.TypeReference     => GetTypeReferenceName((TypeReferenceHandle)_resolutionScope),
                _ => null
            };

            return _resolutionScopeName;
        }
    }

    private string? GetTypeReferenceName(TypeReferenceHandle handle)
    {
        var typeRef = _reader.GetTypeReference(handle);
        var ns             = _reader.GetString(typeRef.Namespace);
        var name                = _reader.GetString(typeRef.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeReferenceSignature"/> class.
    /// </summary>
    public TypeReferenceSignature(MetadataReader reader, TypeReferenceHandle handle)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _handle = handle;

        var typeRef = _reader.GetTypeReference(handle);
        _resolutionScope = typeRef.ResolutionScope;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.TypeReference;

    public override bool? IsValueType => null; // Cannot determine without external resolution

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        var typeRef = _reader.GetTypeReference(_handle);
        var actualNs       = _reader.GetString(typeRef.Namespace);
        var actualName          = _reader.GetString(typeRef.Name);

        if (!string.Equals(actualNs, ns, StringComparison.Ordinal) ||
            !string.Equals(actualName, name, StringComparison.Ordinal))
        {
            return false;
        }

        // If scope is specified, check resolution scope
        if (scope != null)
        {
            return string.Equals(ResolutionScopeName, scope, StringComparison.Ordinal);
        }

        return true;
    }

    public override bool Equals(TypeSignature? other)
    {
        if (other is not TypeReferenceSignature r) { return false; }
        // Same reader + same handle > structurally identical
        if (ReferenceEquals(r._reader, _reader) && (r._handle == _handle)) { return true; }
        // Cross-reader: compare namespace and name strings
        var a = _reader.GetTypeReference(_handle);
        var b = r._reader.GetTypeReference(r._handle);
        return _reader.GetString(a.Namespace) == r._reader.GetString(b.Namespace) &&
               _reader.GetString(a.Name) == r._reader.GetString(b.Name) &&
               (ResolutionScopeName == r.ResolutionScopeName);
    }

    public override int GetHashCode()
    {
        var typeRef = _reader.GetTypeReference(_handle);
        return HashCode.Combine(
            _reader.GetString(typeRef.Namespace),
            _reader.GetString(typeRef.Name),
            ResolutionScopeName);
    }

    public override void FormatTo(StringBuilder sb)
    {
        var typeRef = _reader.GetTypeReference(_handle);
        var ns             = _reader.GetString(typeRef.Namespace);
        var name                = _reader.GetString(typeRef.Name);

        if (!string.IsNullOrEmpty(ns))
        {
            sb.Append(ns);
            sb.Append('.');
        }
        sb.Append(name);
    }
}

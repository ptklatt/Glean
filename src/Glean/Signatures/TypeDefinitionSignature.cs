using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

using Glean.Internal;

namespace Glean.Signatures;

/// <summary>
/// Represents a type definition signature from the current module.
/// Zero allocation wrapper around (MetadataReader, TypeDefinitionHandle).
/// </summary>
public sealed class TypeDefinitionSignature : TypeSignature
{
    private readonly MetadataReader _reader;
    private readonly TypeDefinitionHandle _handle;
    private bool? _isValueType;

    /// <summary>
    /// Gets the metadata reader.
    /// </summary>
    public MetadataReader Reader => _reader;

    /// <summary>
    /// Gets the type definition handle.
    /// </summary>
    public TypeDefinitionHandle Handle => _handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDefinitionSignature"/> class.
    /// </summary>
    public TypeDefinitionSignature(MetadataReader reader, TypeDefinitionHandle handle)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _handle = handle;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.TypeDefinition;

    public override bool? IsValueType
    {
        get
        {
            if (_isValueType.HasValue) { return _isValueType.Value; }

            var typeDef = _reader.GetTypeDefinition(_handle);
            var baseType = typeDef.BaseType;

            if (baseType.IsNil)
            {
                _isValueType = false;
                return false;
            }

            // Check if base type is System.ValueType or System.Enum
            if (baseType.Kind == HandleKind.TypeReference)
            {
                var typeRef = _reader.GetTypeReference((TypeReferenceHandle)baseType);
                var ns = _reader.GetString(typeRef.Namespace);
                var name = _reader.GetString(typeRef.Name);

                _isValueType = string.Equals(ns, WellKnownTypes.SystemNs, StringComparison.Ordinal) &&
                              ((name == WellKnownTypes.ValueType) || (name == WellKnownTypes.Enum));
            }
            else if (baseType.Kind == HandleKind.TypeDefinition)
            {
                var typeDefinition = _reader.GetTypeDefinition((TypeDefinitionHandle)baseType);
                _isValueType = _reader.StringComparer.Equals(typeDefinition.Namespace, WellKnownTypes.SystemNs) &&
                               (_reader.StringComparer.Equals(typeDefinition.Name, WellKnownTypes.ValueType) ||
                                _reader.StringComparer.Equals(typeDefinition.Name, WellKnownTypes.Enum));
            }
            else
            {
                _isValueType = null; // Cannot determine without recursion
            }

            return _isValueType;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        var typeDef = _reader.GetTypeDefinition(_handle);
        if (!_reader.StringComparer.Equals(typeDef.Namespace, ns) ||
            !_reader.StringComparer.Equals(typeDef.Name, name))
        {
            return false;
        }

        if (scope == null)
        {
            return true;
        }

        if (_reader.IsAssembly && _reader.StringComparer.Equals(_reader.GetAssemblyDefinition().Name, scope))
        {
            return true;
        }

        return _reader.StringComparer.Equals(_reader.GetModuleDefinition().Name, scope);
    }

    public override bool Equals(TypeSignature? other)
        => (other is TypeDefinitionSignature t) && (t._handle == _handle) && ReferenceEquals(t._reader, _reader);

    public override int GetHashCode() => HashCode.Combine(_reader, _handle);

    public override void FormatTo(StringBuilder sb)
    {
        var typeDef = _reader.GetTypeDefinition(_handle);
        var ns = _reader.GetString(typeDef.Namespace);
        var name = _reader.GetString(typeDef.Name);

        if (!string.IsNullOrEmpty(ns))
        {
            sb.Append(ns);
            sb.Append('.');
        }
        sb.Append(name);
    }
}

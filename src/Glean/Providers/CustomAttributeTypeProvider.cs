using System.ComponentModel;
using System.Reflection.Metadata;

using Glean.Signatures;

namespace Glean.Providers;

/// <summary>
/// Provides type decoding for custom attributes.
/// </summary>
/// <remarks>
/// Most callers should use <see cref="Decoding.CustomAttributeDecoder"/> or the custom attribute
/// helpers on contexts and extensions. This provider is mainly for raw System.Reflection.Metadata decode pipelines.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class CustomAttributeTypeProvider : ICustomAttributeTypeProvider<TypeSignature>
{
    private readonly MetadataReader _reader;
    private readonly IEnumResolver? _enumResolver;
    // Caches results (including failures) of the O(N) TypeDefinitions scan for serialized name enums.
    // null value = type not found in this reader (will throw on access).
    private readonly Dictionary<string, PrimitiveTypeCode?> _serializedNameEnumCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAttributeTypeProvider"/> class.
    /// </summary>
    public CustomAttributeTypeProvider(MetadataReader reader, IEnumResolver? enumResolver = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _enumResolver = enumResolver;
    }

    /// <inheritdoc/>
    public TypeSignature GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return PrimitiveTypeSignature.Get(typeCode);
    }

    /// <inheritdoc/>
    public TypeSignature GetSystemType()
    {
        return new SerializedTypeNameSignature("System.Type");
    }

    /// <inheritdoc/>
    public TypeSignature GetSZArrayType(TypeSignature elementType)
    {
        return new SZArraySignature(elementType);
    }

    /// <inheritdoc/>
    public TypeSignature GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return new TypeDefinitionSignature(reader, handle);
    }

    /// <inheritdoc/>
    public TypeSignature GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return new TypeReferenceSignature(reader, handle);
    }

    /// <summary>Wraps the serialized type name without resolving it.</summary>
    public TypeSignature GetTypeFromSerializedName(string name)
    {
        return new SerializedTypeNameSignature(name);
    }

    /// <summary>Finds the enum storage type locally, or uses the enum resolver for type references.</summary>
    /// <exception cref="BadImageFormatException">The enum storage type cannot be resolved.</exception>
    public PrimitiveTypeCode GetUnderlyingEnumType(TypeSignature type)
    {
        if (type is SerializedTypeNameSignature serialized)
        {
            string typeName = serialized.SerializedName;
            int comma       = typeName.IndexOf(',', StringComparison.Ordinal);
            if (comma >= 0)
            {
                typeName = typeName.Substring(0, comma);
            }

            typeName = typeName.Trim();

            if (_serializedNameEnumCache.TryGetValue(typeName, out var cached))
            {
                if (cached.HasValue)
                {
                    return cached.Value;
                }
                // Known failure - fall through to throw below.
            }
            else
            {
                if (IsSimpleSerializedName(typeName) &&
                    TypeName.TryParse(typeName, out var parsedName, new TypeNameParseOptions { MaxNodes = typeName.Length }) &&
                    (parsedName.IsSimple))
                {
                    var names = new List<string>();
                    while (parsedName.IsNested)
                    {
                        names.Add(TypeName.Unescape(parsedName.Name));
                        parsedName = parsedName.DeclaringType;
                    }
                    names.Add(TypeName.Unescape(parsedName.Name));
                    string nameSpace = TypeName.Unescape(parsedName.Namespace);

                    foreach (var tdHandle in _reader.TypeDefinitions)
                    {
                        var td = _reader.GetTypeDefinition(tdHandle);
                        if (!MatchesSerializedName(td, nameSpace, names))
                        {
                            continue;
                        }

                        foreach (var fieldHandle in td.GetFields())
                        {
                            var field = _reader.GetFieldDefinition(fieldHandle);
                            if (!_reader.StringComparer.Equals(field.Name, "value__")) { continue; }

                            var sig = field.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                            if (sig is PrimitiveTypeSignature prim)
                            {
                                _serializedNameEnumCache[typeName] = prim.TypeCode;
                                return prim.TypeCode;
                            }
                        }
                    }
                }

                // Not found in this reader; cache the failure.
                _serializedNameEnumCache[typeName] = null;
            }
        }

        // Try local resolution for TypeDefinition
        if (type is TypeDefinitionSignature typeDef)
        {
            var def = typeDef.Reader.GetTypeDefinition(typeDef.Handle);
            foreach (var fieldHandle in def.GetFields())
            {
                var field = typeDef.Reader.GetFieldDefinition(fieldHandle);
                var fieldName = typeDef.Reader.GetString(field.Name);

                if (fieldName == "value__")
                {
                    var sig = field.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                    if (sig is PrimitiveTypeSignature prim)
                    {
                        return prim.TypeCode;
                    }
                }
            }
        }

        // Try external resolution for TypeReference
        if (type is TypeReferenceSignature typeRef && _enumResolver != null)
        {
            var resolved = _enumResolver.Resolve(typeRef);
            if (resolved.HasValue)
            {
                return resolved.Value;
            }
        }

        throw new BadImageFormatException($"Cannot resolve underlying enum type for {type}");
    }

    private static bool IsSimpleSerializedName(string name)
    {
        bool escaped = false;
        foreach (char character in name)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                escaped = true;
                continue;
            }
            if ((character == '[') || (character == ']') || (character == '*') || (character == '&'))
            {
                return false;
            }
        }
        return (name.Length != 0) && !escaped;
    }

    private bool MatchesSerializedName(TypeDefinition definition, string nameSpace, List<string> names)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (!_reader.StringComparer.Equals(definition.Name, names[i]))
            {
                return false;
            }
            if (i == names.Count - 1)
            {
                return (!definition.IsNested) && _reader.StringComparer.Equals(definition.Namespace, nameSpace);
            }
            if (!definition.IsNested)
            {
                return false;
            }
            var parent = definition.GetDeclaringType();
            if (parent.IsNil)
            {
                return false;
            }
            definition = _reader.GetTypeDefinition(parent);
        }
        return false;
    }

    /// <inheritdoc/>
    public bool IsSystemType(TypeSignature type)
    {
        bool result = false;
        if (type is SerializedTypeNameSignature serialized)
        {
            result = serialized.SerializedName == "System.Type";
        }
        else if (type is TypeDefinitionSignature typeDef)
        {
            var def = typeDef.Reader.GetTypeDefinition(typeDef.Handle);
            {
                result = typeDef.Reader.StringComparer.Equals(def.Namespace, "System") &&
                         typeDef.Reader.StringComparer.Equals(def.Name, "Type");
                
            }
        }
        else if (type is TypeReferenceSignature typeRef)
        {
            var tr = typeRef.Reader.GetTypeReference(typeRef.Handle);
            {
                result = typeRef.Reader.StringComparer.Equals(tr.Namespace, "System") &&
                         typeRef.Reader.StringComparer.Equals(tr.Name, "Type");
            }
        }

        return result;
    }
}

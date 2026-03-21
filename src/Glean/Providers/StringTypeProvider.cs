using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Providers;

/// <summary>
/// A simple string-based signature type provider for readable output.
/// </summary>
/// <remarks>
/// Formats signatures into C#-like strings for debugging/tests. This API allocates.
/// </remarks>
public sealed class StringTypeProvider : ISignatureTypeProvider<string, int>, ICustomAttributeTypeProvider<string>
{
    /// <summary>
    /// Singleton instance of the provider.
    /// </summary>
    public static readonly StringTypeProvider Instance = new();

    /// <summary>
    /// Empty generic context (int = 0).
    /// </summary>
    public static readonly int EmptyContext = 0;

    private StringTypeProvider() { }

    // Primitive types

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Void           => "void",
        PrimitiveTypeCode.Boolean        => "bool",
        PrimitiveTypeCode.Char           => "char",
        PrimitiveTypeCode.SByte          => "sbyte",
        PrimitiveTypeCode.Byte           => "byte",
        PrimitiveTypeCode.Int16          => "short",
        PrimitiveTypeCode.UInt16         => "ushort",
        PrimitiveTypeCode.Int32          => "int",
        PrimitiveTypeCode.UInt32         => "uint",
        PrimitiveTypeCode.Int64          => "long",
        PrimitiveTypeCode.UInt64         => "ulong",
        PrimitiveTypeCode.Single         => "float",
        PrimitiveTypeCode.Double         => "double",
        PrimitiveTypeCode.String         => "string",
        PrimitiveTypeCode.Object         => "object",
        PrimitiveTypeCode.IntPtr         => "nint",
        PrimitiveTypeCode.UIntPtr        => "nuint",
        PrimitiveTypeCode.TypedReference => "TypedReference",
        _                                => typeCode.ToString()
    };

    // Array types

    /// <inheritdoc/>
    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    /// <inheritdoc/>
    public string GetArrayType(string elementType, ArrayShape shape)
    {
        if (shape.Rank == 1)
        {
            return $"{elementType}[*]";
        }

        var sb = new StringBuilder(elementType);
        sb.Append('[');
        for (int i = 1; i < shape.Rank; i++)
        {
            sb.Append(',');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // Reference and pointer types

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetByReferenceType(string elementType) => $"ref {elementType}";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetPointerType(string elementType) => $"{elementType}*";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetPinnedType(string elementType) => elementType;

    // Generic types

    /// <inheritdoc/>
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        var tickIndex = genericType.IndexOf('`');
        var baseName = tickIndex >= 0 ? genericType.Substring(0, tickIndex) : genericType;

        var sb = new StringBuilder(baseName);
        sb.Append('<');
        for (int i = 0; i < typeArguments.Length; i++)
        {
            if (i > 0) { sb.Append(", "); }
            sb.Append(typeArguments[i]);
        }
        sb.Append('>');
        return sb.ToString();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetGenericTypeParameter(int genericContext, int index) => $"T{index}";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetGenericMethodParameter(int genericContext, int index) => $"TM{index}";

    // Type references

    /// <inheritdoc/>
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var name = reader.GetString(typeDef.Name);
        var ns = reader.GetString(typeDef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <inheritdoc/>
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var typeRef = reader.GetTypeReference(handle);
        var name = reader.GetString(typeRef.Name);
        var ns = reader.GetString(typeRef.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <inheritdoc/>
    public string GetTypeFromSpecification(MetadataReader reader, int genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var typeSpec = reader.GetTypeSpecification(handle);
        return typeSpec.DecodeSignature(this, genericContext);
    }

    // Modified types

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
        => unmodifiedType;

    // Function pointers

    /// <inheritdoc/>
    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        var sb = new StringBuilder("delegate*<");
        for (int i = 0; i < signature.ParameterTypes.Length; i++)
        {
            sb.Append(signature.ParameterTypes[i]);
            sb.Append(", ");
        }
        sb.Append(signature.ReturnType);
        sb.Append('>');
        return sb.ToString();
    }

    // Custom attribute provider

    /// <inheritdoc/>
    public string GetSystemType() => "System.Type";

    /// <inheritdoc/>
    public bool IsSystemType(string type) => (type == "System.Type") || (type == "Type");

    /// <inheritdoc/>
    public string GetTypeFromSerializedName(string name) => name;

    /// <inheritdoc/>
    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;

    // Helpers

    /// <summary>
    /// Formats a method signature as a readable string.
    /// </summary>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="method">The method definition.</param>
    /// <param name="includeReturnType">Whether to include the return type.</param>
    /// <returns>A formatted method signature string.</returns>
    public static string FormatMethodSignature(MetadataReader reader, MethodDefinition method, bool includeReturnType)
    {
        var sig = method.DecodeSignature(Instance, EmptyContext);
        var name = reader.GetString(method.Name);

        var sb = new StringBuilder();
        if (includeReturnType)
        {
            sb.Append(sig.ReturnType);
            sb.Append(' ');
        }

        sb.Append(name);

        // Check for generic parameters
        var genericParams = method.GetGenericParameters();
        if (genericParams.Count > 0)
        {
            sb.Append('<');
            bool first = true;
            foreach (var gpHandle in genericParams)
            {
                if (!first) { sb.Append(", "); }
                first = false;
                var gp = reader.GetGenericParameter(gpHandle);
                sb.Append(reader.GetString(gp.Name));
            }
            sb.Append('>');
        }

        sb.Append('(');
        for (int i = 0; i < sig.ParameterTypes.Length; i++)
        {
            if (i > 0) { sb.Append(", "); }
            sb.Append(sig.ParameterTypes[i]);
        }
        sb.Append(')');

        return sb.ToString();
    }
}

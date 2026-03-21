using System.Reflection.Metadata;

using Glean.Providers;
using Glean.Signatures;

namespace Glean.Extensions;

/// <summary>
/// Advanced extensions for TypeSpecification analysis.
/// </summary>
public static class TypeSpecificationExtensions
{
    /// <summary>
    /// Decodes a TypeSpecification into a TypeSignature.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>The decoded type signature.</returns>
    public static TypeSignature DecodeTypeSpecification(this TypeSpecificationHandle handle, MetadataReader reader)
    {
        var provider = SignatureTypeProvider.Instance;
        var genericContext = SignatureDecodeContext.Empty;

        EntityHandle entityHandle = handle;
        return entityHandle.DecodeTypeSignature(reader, provider, genericContext);
    }

    /// <summary>
    /// Checks if a TypeSpecification represents a generic instantiation.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type is a generic instantiation like List&lt;int&gt;.</returns>
    public static bool IsGenericInstantiation(this TypeSpecificationHandle handle, MetadataReader reader)
    {
        var signature = handle.DecodeTypeSpecification(reader);
        return signature is GenericInstanceSignature;
    }

    /// <summary>
    /// Checks if a TypeSpecification represents an array type.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type is an array (single or multi dimensional).</returns>
    public static bool IsArrayType(this TypeSpecificationHandle handle, MetadataReader reader)
    {
        var signature = handle.DecodeTypeSpecification(reader);
        return signature is SZArraySignature or ArraySignature;
    }

    /// <summary>
    /// Checks if a TypeSpecification represents a pointer type.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type is a pointer like int*.</returns>
    public static bool IsPointerType(this TypeSpecificationHandle handle, MetadataReader reader)
    {
        var signature = handle.DecodeTypeSpecification(reader);
        return signature is PointerSignature;
    }

    /// <summary>
    /// Checks if a TypeSpecification represents a by reference type.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>True if the type is a reference like ref int or out string.</returns>
    public static bool IsByRefType(this TypeSpecificationHandle handle, MetadataReader reader)
    {
        var signature = handle.DecodeTypeSpecification(reader);
        return signature is ByRefSignature;
    }

    /// <summary>
    /// Gets the element type for array, pointer, or by reference types.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>The element type, or null if not a compound type.</returns>
    /// <remarks>
    /// For int[], returns int.
    /// For int*, returns int.
    /// For ref string, returns string.
    /// For List&lt;int&gt;, returns null (use GetGenericArguments instead).
    /// </remarks>
    public static TypeSignature? GetElementType(this TypeSpecificationHandle handle, MetadataReader reader)
    {
        var signature = handle.DecodeTypeSpecification(reader);

        return signature switch
        {
            SZArraySignature szArray => szArray.ElementType,
            ArraySignature array     => array.ElementType,
            PointerSignature pointer => pointer.ElementType,
            ByRefSignature byRef     => byRef.ElementType,
            _                        => null
        };
    }

    /// <summary>
    /// Gets the generic type arguments for a generic instantiation.
    /// </summary>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <returns>The generic type arguments, or null if not a generic instantiation.</returns>
    /// <remarks>
    /// For List&lt;int&gt;, returns [int].
    /// For Dictionary&lt;string, object&gt;, returns [string, object].
    /// For non generic types, returns null.
    /// </remarks>
    public static System.Collections.Immutable.ImmutableArray<TypeSignature>? GetGenericArguments(this TypeSpecificationHandle handle,
        MetadataReader reader)
    {
        var signature = handle.DecodeTypeSpecification(reader);

        if (signature is GenericInstanceSignature genericInstance)
        {
            return genericInstance.Arguments;
        }

        return null;
    }
}

using System.ComponentModel;
using System.Collections.Immutable;
using System.Reflection.Metadata;

using Glean.Signatures;

namespace Glean.Providers;

/// <summary>
/// Provides type signature decoding for managed assembly metadata.
/// Rich decoding provider that materializes <see cref="TypeSignature"/> nodes.
/// </summary>
/// <remarks>
/// Most callers should use higher level helpers such as context <c>Decode*</c> members or
/// extension methods and only reach for this provider when staying on the raw System.Reflection.Metadata decode APIs.
/// <para/>
/// Primitive signatures are reused via singletons where possible, but most non primitive
/// decode paths allocate signature objects (arrays, generic instantiations, modifiers, etc.).
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class SignatureTypeProvider : ISignatureTypeProvider<TypeSignature, SignatureDecodeContext>
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SignatureTypeProvider Instance { get; } = new();

    private SignatureTypeProvider() { }

    public TypeSignature GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return PrimitiveTypeSignature.Get(typeCode);
    }

    public TypeSignature GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return new TypeDefinitionSignature(reader, handle);
    }

    public TypeSignature GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return new TypeReferenceSignature(reader, handle);
    }

    public TypeSignature GetTypeFromSpecification(MetadataReader reader, SignatureDecodeContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        // Recurse: decode the type specification
        var typeSpec = reader.GetTypeSpecification(handle);
        return typeSpec.DecodeSignature(this, genericContext);
    }

    public TypeSignature GetSZArrayType(TypeSignature elementType)
    {
        return new SZArraySignature(elementType);
    }

    public TypeSignature GetArrayType(TypeSignature elementType, ArrayShape shape)
    {
        return new ArraySignature(elementType, shape);
    }

    public TypeSignature GetByReferenceType(TypeSignature elementType)
    {
        return new ByRefSignature(elementType);
    }

    public TypeSignature GetPointerType(TypeSignature elementType)
    {
        return new PointerSignature(elementType);
    }

    public TypeSignature GetGenericInstantiation(TypeSignature genericType, ImmutableArray<TypeSignature> typeArguments)
    {
        return new GenericInstanceSignature(genericType, typeArguments);
    }

    public TypeSignature GetGenericTypeParameter(SignatureDecodeContext genericContext, int index)
    {
        // If context has type arguments, substitute
        if (!genericContext.TypeArguments.IsDefaultOrEmpty && (index < genericContext.TypeArguments.Length))
        {
            return genericContext.TypeArguments[index];
        }

        return new GenericTypeParameterSignature(index);
    }

    public TypeSignature GetGenericMethodParameter(SignatureDecodeContext genericContext, int index)
    {
        // If context has method arguments, substitute
        if (!genericContext.MethodArguments.IsDefaultOrEmpty && (index < genericContext.MethodArguments.Length))
        {
            return genericContext.MethodArguments[index];
        }

        return new GenericMethodParameterSignature(index);
    }

    public TypeSignature GetPinnedType(TypeSignature elementType)
    {
        return new PinnedTypeSignature(elementType);
    }

    public TypeSignature GetModifiedType(TypeSignature modifier, TypeSignature unmodifiedType, bool isRequired)
    {
        TypeSignature result;
        
        // Accumulate modifiers if the unmodified type is already modified
        if (unmodifiedType is ModifiedTypeSignature modifiedType)
        {
            var requiredMods = isRequired 
                ? modifiedType.RequiredModifiers.Add(modifier) 
                : modifiedType.RequiredModifiers;

            var optionalMods = isRequired
                ? modifiedType.OptionalModifiers
                : modifiedType.OptionalModifiers.Add(modifier);

            result = new ModifiedTypeSignature(modifiedType.UnmodifiedType, requiredMods, optionalMods);
        }
        else
        {
            var reqMods = isRequired 
                ? ImmutableArray.Create(modifier) 
                : ImmutableArray<TypeSignature>.Empty;
            
            var optMods = isRequired 
                ? ImmutableArray<TypeSignature>.Empty 
                : ImmutableArray.Create(modifier);
            
            result = new ModifiedTypeSignature(unmodifiedType, reqMods, optMods);
        }
        
        return result;
    }

    public TypeSignature GetFunctionPointerType(MethodSignature<TypeSignature> signature)
    {
        return new FunctionPointerSignature(signature);
    }

    public TypeSignature GetSentinelType()
    {
        return SentinelTypeSignature.Instance;
    }
}

using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="EntityHandle"/> that bridge to Signatures infrastructure.
/// </summary>
/// <remarks>
/// Advanced: bridges to the <see cref="Signatures.TypeSignature"/> hierarchy.
/// Prefer context level APIs for most scenarios; use this class when you need
/// direct <see cref="Signatures.TypeSignature"/> decoding from a raw <see cref="EntityHandle"/>.
/// </remarks>
public static class EntityHandleExtensions
{
    // Type signature decoding

    /// <summary>
    /// Decodes an EntityHandle to a type signature using the Core TypeSignature infrastructure.
    /// This bridges Extensions with Signatures for advanced type analysis.
    /// </summary>
    /// <remarks>
    /// This method provides access to the TypeSignature hierarchy (PrimitiveTypeSignature,
    /// TypeReferenceSignature, GenericInstanceSignature, etc.) which is essential for:
    /// - Generic type substitution
    /// - Cross assembly type resolution
    /// - Interface implementation analysis
    /// - Constraint analysis
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Signatures.TypeSignature DecodeTypeSignature(
        this EntityHandle handle,
        MetadataReader reader)
    {
        return DecodeTypeSignatureCore(handle, reader, Providers.SignatureTypeProvider.Instance, Providers.SignatureDecodeContext.Empty);
    }

    /// <summary>
    /// Decodes an EntityHandle to a type signature with a custom provider and generic context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Signatures.TypeSignature DecodeTypeSignature(
        this EntityHandle handle,
        MetadataReader reader,
        ISignatureTypeProvider<Signatures.TypeSignature, Providers.SignatureDecodeContext> provider,
        Providers.SignatureDecodeContext genericContext)
    {
        return DecodeTypeSignatureCore(handle, reader, provider, genericContext);
    }

    private static Signatures.TypeSignature DecodeTypeSignatureCore(
        EntityHandle handle,
        MetadataReader reader,
        ISignatureTypeProvider<Signatures.TypeSignature, Providers.SignatureDecodeContext> provider,
        Providers.SignatureDecodeContext genericContext)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return provider.GetTypeFromDefinition(reader, (TypeDefinitionHandle)handle, 0);

            case HandleKind.TypeReference:
                return provider.GetTypeFromReference(reader, (TypeReferenceHandle)handle, 0);

            case HandleKind.TypeSpecification:
                return provider.GetTypeFromSpecification(reader, genericContext, (TypeSpecificationHandle)handle, 0);

            default:
                throw new BadImageFormatException($"Cannot decode entity handle of kind {handle.Kind} as a type signature");
        }
    }
}

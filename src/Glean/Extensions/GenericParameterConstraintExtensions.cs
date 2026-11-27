using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extensions for upgrading fast tier generic constraint metadata to decoded signatures.
/// </summary>
/// <remarks>
/// Advanced: bridges <see cref="GenericParameterConstraint"/> to the
/// <see cref="Signatures.TypeSignature"/> hierarchy. Prefer <see cref="GenericParameterExtensions"/>
/// for common generic parameter analysis; use this only when you need decoded constraint signatures.
/// </remarks>
public static class GenericParameterConstraintExtensions
{
    /// <summary>
    /// Decodes a generic constraint type to a TypeSignature.
    /// This is a rich tier helper and may allocate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Signatures.TypeSignature DecodeConstraintTypeSignature(
        this GenericParameterConstraint constraint,
        MetadataReader reader)
    {
        var provider = Providers.SignatureTypeProvider.Instance;
        return constraint.Type.DecodeTypeSignature(reader, provider, Providers.SignatureDecodeContext.Empty);
    }

    /// <summary>Decodes a constraint type using the supplied provider and generic context. Decoding may allocate.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="provider"/> is null.</exception>
    /// <exception cref="BadImageFormatException">The constraint does not identify a supported type.</exception>
    public static TType DecodeConstraintTypeSignature<TType, TContext>(
        this GenericParameterConstraint constraint,
        MetadataReader reader,
        ISignatureTypeProvider<TType, TContext> provider,
        TContext genericContext)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (provider == null) { throw new ArgumentNullException(nameof(provider)); }

        var type = constraint.Type;
        if (type.IsNil) { throw new BadImageFormatException("Constraint type cannot be nil."); }

        return type.Kind switch
        {
            HandleKind.TypeDefinition => provider.GetTypeFromDefinition(reader, (TypeDefinitionHandle)type, 0),
            HandleKind.TypeReference => provider.GetTypeFromReference(reader, (TypeReferenceHandle)type, 0),
            HandleKind.TypeSpecification => provider.GetTypeFromSpecification(reader, genericContext, (TypeSpecificationHandle)type, 0),
            _ => throw new BadImageFormatException($"Cannot decode constraint type of kind {type.Kind}.")
        };
    }
}

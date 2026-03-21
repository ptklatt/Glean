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
}

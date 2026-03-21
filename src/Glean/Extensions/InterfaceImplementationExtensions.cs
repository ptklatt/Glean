using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extensions for upgrading fast tier interface metadata to decoded signatures.
/// </summary>
/// <remarks>
/// Advanced: bridges <see cref="InterfaceImplementation"/> to the
/// <see cref="Signatures.TypeSignature"/> hierarchy.
/// For checking whether a type implements an interface by name, prefer
/// <see cref="TypeDefinitionExtensions"/>.
/// </remarks>
public static class InterfaceImplementationExtensions
{
    /// <summary>
    /// Decodes the implemented interface type to a TypeSignature.
    /// This is a rich tier helper and may allocate.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Signatures.TypeSignature DecodeInterfaceTypeSignature(
        this InterfaceImplementation interfaceImplementation,
        MetadataReader reader)
    {
        var provider = Providers.SignatureTypeProvider.Instance;
        return interfaceImplementation.Interface.DecodeTypeSignature(reader, provider, Providers.SignatureDecodeContext.Empty);
    }
}

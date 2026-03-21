using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="MemberReference"/>.
/// Provides signature decoding based on member kind.
/// </summary>
/// <remarks>
/// Raw <see cref="MemberReference"/> work (signature kind detection, field/method signature decoding).
/// </remarks>
public static class MemberReferenceExtensions
{
    /// <summary>
    /// Gets the signature kind for a member reference (method or field).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SignatureKind GetSignatureKind(this MemberReference memberRef, MetadataReader reader)
    {
        var signature = reader.GetBlobReader(memberRef.Signature);
        var header = signature.ReadSignatureHeader();
        return header.Kind;
    }

    /// <summary>
    /// Gets whether the member reference is a method (vs a field).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMethodReference(this MemberReference memberRef, MetadataReader reader)
    {
        return memberRef.GetSignatureKind(reader) == SignatureKind.Method;
    }

    /// <summary>
    /// Gets whether the member reference is a field (vs a method).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFieldReference(this MemberReference memberRef, MetadataReader reader)
    {
        return memberRef.GetSignatureKind(reader) == SignatureKind.Field;
    }

    /// <summary>
    /// Decodes the field signature from a member reference.
    /// </summary>
    /// <exception cref="BadImageFormatException">Thrown if the member reference is not a field.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TType DecodeFieldSignature<TType, TSignatureDecodeContext>(
        this MemberReference memberRef,
        ISignatureTypeProvider<TType, TSignatureDecodeContext> provider,
        TSignatureDecodeContext genericContext)
    {
        var kind = memberRef.GetKind();
        if (kind != MemberReferenceKind.Field)
        {
            throw new BadImageFormatException($"Expected field signature, but member reference kind is {kind}");
        }

        return memberRef.DecodeFieldSignature(provider, genericContext);
    }

    /// <summary>
    /// Decodes the method signature from a member reference.
    /// </summary>
    /// <exception cref="BadImageFormatException">Thrown if the member reference is not a method.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodSignature<TType> DecodeMethodSignature<TType, TSignatureDecodeContext>(
        this MemberReference memberRef,
        ISignatureTypeProvider<TType, TSignatureDecodeContext> provider,
        TSignatureDecodeContext genericContext)
    {
        var kind = memberRef.GetKind();
        if (kind != MemberReferenceKind.Method)
        {
            throw new BadImageFormatException($"Expected method signature, but member reference kind is {kind}");
        }

        return memberRef.DecodeMethodSignature(provider, genericContext);
    }

    /// <summary>
    /// Checks if the member name matches.
    /// Zero allocation identity check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this MemberReference memberRef, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(memberRef.Name, name);
    }
}

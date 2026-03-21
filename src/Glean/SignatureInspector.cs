using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean;

/// <summary>
/// Fast tier signature inspector. Reads signature blob shapes directly via <see cref="BlobReader"/>
/// without constructing <c>TypeSignature</c> object graphs or allocating.
/// </summary>
/// <remarks>
/// Use these methods for common pattern checks (is this an indexer? what is the generic arity?)
/// where the full rich tier decode via <c>DecodeSignature</c> would be wasteful.
/// All methods are zero allocation on the hot path.
/// </remarks>
public static class SignatureInspector
{
    // ECMA-335 §II.23.2.5: PropertySig = PROPERTY [HASTHIS] ParamCount RetType Param*
    // First byte is 0x08 (PROPERTY) or 0x28 (PROPERTY | HASTHIS).
    // ParamCount follows immediately as a compressed integer.

    /// <summary>
    /// Reads the parameter count from a property signature blob without allocating.
    /// Equivalent to decoding the full property signature and checking
    /// <c>MethodSignature&lt;T&gt;.ParameterTypes.Length</c>, but without constructing the object graph.
    /// </summary>
    /// <param name="reader">The metadata reader that owns the blob.</param>
    /// <param name="signatureBlob">The property signature blob handle (from <c>PropertyDefinition.Signature</c>).</param>
    /// <param name="parameterCount">
    /// When this method returns <see langword="true"/>, contains the number of index parameters.
    /// A value of 0 means a simple property (not an indexer).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the blob was successfully read; <see langword="false"/> if the blob is
    /// nil or malformed.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetPropertyParameterCount(
        MetadataReader reader,
        BlobHandle signatureBlob,
        out int parameterCount)
    {
        if (signatureBlob.IsNil)
        {
            parameterCount = 0;
            return false;
        }

        try
        {
            var blob = reader.GetBlobReader(signatureBlob);
            blob.ReadByte(); // skip PROPERTY / PROPERTY+HASTHIS header byte
            parameterCount = blob.ReadCompressedInteger();
            return true;
        }
        catch (BadImageFormatException)
        {
            parameterCount = 0;
            return false;
        }
    }

    /// <summary>
    /// Reads the leading element type code from the current position in a blob reader,
    /// advancing the reader by one byte.
    /// </summary>
    /// <param name="blobReader">
    /// A blob reader positioned at the start of a type signature
    /// (e.g., a <c>TypeSpecification.Signature</c> blob, or the type portion of a field/method blob
    /// after any blob kind header bytes have been consumed).
    /// </param>
    /// <returns>
    /// The <see cref="SignatureTypeCode"/> of the leading type. Common values:
    /// <list type="bullet">
    ///   <item><c>Class</c> or <c>ValueType</c>: named type</item>
    ///   <item><see cref="SignatureTypeCode.GenericTypeInstance"/>: generic instantiation — follow with <see cref="TryGetGenericInstanceArity"/></item>
    ///   <item><see cref="SignatureTypeCode.SZArray"/> or <see cref="SignatureTypeCode.Array"/>: array type</item>
    ///   <item><see cref="SignatureTypeCode.ByReference"/>: byref type</item>
    ///   <item><see cref="SignatureTypeCode.Pointer"/>: pointer type</item>
    ///   <item>Any primitive code (e.g., <see cref="SignatureTypeCode.Int32"/>): primitive type</item>
    /// </list>
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SignatureTypeCode PeekTypeCode(ref BlobReader blobReader)
    {
        return (SignatureTypeCode)blobReader.ReadByte();
    }

    /// <summary>
    /// Reads the generic argument count from a <c>ELEMENT_TYPE_GENERICINST</c> type signature blob
    /// (e.g., a <c>TypeSpecification.Signature</c> blob for a constructed generic type).
    /// </summary>
    /// <param name="reader">The metadata reader that owns the blob.</param>
    /// <param name="typeSignatureBlob">
    /// A type signature blob that begins with the element type byte (no blob kind header).
    /// Typically <c>TypeSpecification.Signature</c>.
    /// </param>
    /// <param name="arity">
    /// When this method returns <see langword="true"/>, contains the number of generic arguments
    /// (e.g., 1 for <c>IList&lt;int&gt;</c>, 2 for <c>Dictionary&lt;K,V&gt;</c>).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the blob leads with <c>ELEMENT_TYPE_GENERICINST</c> and the arity
    /// was successfully read; <see langword="false"/> if the blob is nil, does not start with
    /// <c>GENERICINST</c>, or is malformed.
    /// </returns>
    public static bool TryGetGenericInstanceArity(
        MetadataReader reader,
        BlobHandle typeSignatureBlob,
        out int arity)
    {
        if (typeSignatureBlob.IsNil)
        {
            arity = 0;
            return false;
        }

        try
        {
            var blob = reader.GetBlobReader(typeSignatureBlob);

            // ELEMENT_TYPE_GENERICINST (0x15) [class|valuetype] TypeDefOrRefEncoded GenArgCount
            if ((SignatureTypeCode)blob.ReadByte() != SignatureTypeCode.GenericTypeInstance)
            {
                arity = 0;
                return false;
            }

            blob.ReadByte();               // class (0x12) or valuetype (0x11) byte
            blob.ReadCompressedInteger();  // TypeDefOrRefEncoded token (variable length)
            arity = blob.ReadCompressedInteger();
            return true;
        }
        catch (BadImageFormatException)
        {
            arity = 0;
            return false;
        }
    }
}

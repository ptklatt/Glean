using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Glean.Decoding;

internal static class TypeSpecificationSignatureDecoder
{
    // TypeDefOrRef coded indexes use the low 2 bits as the tag.
    private const int TypeDefOrRefTagBitCount = 2;
    private const int TypeDefOrRefTagMask = 0x3;
    private const int TypeDefinitionTag = 0;
    private const int TypeReferenceTag = 1;
    private const int TypeSpecificationTag = 2;

    private const byte ElementTypeValueType = 0x11;
    private const byte ElementTypeClass = 0x12;

    public static bool TryGetGenericTypeDefinitionHandle(
        MetadataReader reader,
        TypeSpecificationHandle typeSpecificationHandle,
        out EntityHandle genericTypeDefinitionHandle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        if (typeSpecificationHandle.IsNil)
        {
            genericTypeDefinitionHandle = default;
            return false;
        }

        try
        {
            var typeSpec = reader.GetTypeSpecification(typeSpecificationHandle);
            var blobReader = reader.GetBlobReader(typeSpec.Signature);

            // TypeSpec signatures are type signatures with no calling convention header.
            // We only need to peel enough of the blob to recover the generic type definition handle.
            var firstByte = blobReader.ReadByte();
            if (firstByte is ElementTypeClass or ElementTypeValueType)
            {
                return TryReadTypeDefOrRefEncoded(ref blobReader, out genericTypeDefinitionHandle);
            }

            switch ((SignatureTypeCode)firstByte)
            {
                case SignatureTypeCode.GenericTypeInstance:
                {
                    var kindByte = blobReader.ReadByte();
                    if (kindByte is not (ElementTypeClass or ElementTypeValueType))
                    {
                        genericTypeDefinitionHandle = default;
                        return false;
                    }

                    return TryReadTypeDefOrRefEncoded(ref blobReader, out genericTypeDefinitionHandle);
                }

                default:
                    genericTypeDefinitionHandle = default;
                    return false;
            }
        }
        catch (BadImageFormatException)
        {
            genericTypeDefinitionHandle = default;
            return false;
        }
    }

    private static bool TryReadTypeDefOrRefEncoded(ref BlobReader reader, out EntityHandle handle)
    {
        int codedIndex = reader.ReadCompressedInteger();
        int tag = codedIndex & TypeDefOrRefTagMask;
        int rowId = codedIndex >> TypeDefOrRefTagBitCount;

        handle = tag switch
        {
            TypeDefinitionTag => MetadataTokens.TypeDefinitionHandle(rowId),
            TypeReferenceTag => MetadataTokens.TypeReferenceHandle(rowId),
            TypeSpecificationTag => MetadataTokens.TypeSpecificationHandle(rowId),
            _ => default
        };

        return !handle.IsNil;
    }
}

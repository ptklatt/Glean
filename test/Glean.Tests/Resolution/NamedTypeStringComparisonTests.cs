using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using Xunit;

using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Tests.Resolution;

public sealed class NamedTypeStringComparisonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedTypes_InvalidUtf8AndReplacementText_CompareByDecodedValue(bool nameSpace)
    {
        using var definition = Create(false, nameSpace ? "\ufffdpace" : "Space", nameSpace ? "Widget" : "\ufffdidget");
        using var reference = Create(true, "Space", "Widget", invalidUtf8: nameSpace ? 1 : 0);
        var left = new TypeDefinitionSignature(definition.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(reference.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));

        var referenceType = right.Reader.GetTypeReference(right.Handle);
        Assert.Equal(nameSpace ? "\ufffdpace" : "Space", right.Reader.GetString(referenceType.Namespace));
        Assert.Equal(nameSpace ? "Widget" : "\ufffdidget", right.Reader.GetString(referenceType.Name));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(right, left));
    }

    [Theory]
    [InlineData("Exämple", "Wídget", true)]
    [InlineData("Exämple", "wídget", false)]
    [InlineData("Exa\u0308mple", "Wídget", false)]
    public void NamedTypes_NonAsciiNames_PreserveOrdinalComparison(string nameSpace, string name, bool expected)
    {
        using var definition = Create(false, "Exämple", "Wídget");
        using var reference = Create(true, nameSpace, name);
        var left = new TypeDefinitionSignature(definition.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(reference.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));

        Assert.Equal(expected, SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.Equal(expected, SignatureComparison.TypeSignaturesEquivalent(right, left));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedTypes_CustomNameDecoderChanges_PreserveResultsAndExceptions(bool customReference)
    {
        using var definition = Create(false, "Space", "Widget");
        using var reference = Create(true, "Space", "Widget");
        var decoder = new ChangingNameDecoder();
        var left = new TypeDefinitionSignature(customReference ? definition.GetMetadataReader() :
            definition.GetMetadataReader(MetadataReaderOptions.Default, decoder), MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(customReference ? reference.GetMetadataReader(MetadataReaderOptions.Default, decoder) :
            reference.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));

        Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right));
        decoder.Change = true;
        Assert.False(SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.False(SignatureComparison.TypeSignaturesEquivalent(right, left));
        decoder.Throw = true;
        Assert.Throws<InvalidOperationException>(() => SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.Throws<InvalidOperationException>(() => SignatureComparison.TypeSignaturesEquivalent(right, left));
        decoder.Throw = false;
        decoder.Change = false;
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(right, left));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NamedTypes_InvalidStringOffset_ThrowsDespiteOtherNameMismatch(bool definitionInvalid, bool nameSpace)
    {
        using var definition = Create(false, definitionInvalid ? "Space" : "OtherSpace", "Widget",
            invalidOffset: definitionInvalid ? (nameSpace ? 1 : 0) : -1);
        using var reference = Create(true, definitionInvalid ? "OtherSpace" : "Space", "OtherWidget",
            invalidOffset: definitionInvalid ? -1 : (nameSpace ? 1 : 0));
        var left = new TypeDefinitionSignature(definition.GetMetadataReader(), MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(reference.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));

        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(right, left));
    }

    private static MetadataReaderProvider Create(bool reference, string nameSpace, string name, int invalidUtf8 = -1, int invalidOffset = -1)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("sample"), default, default, default);
        metadata.AddAssembly(metadata.GetOrAddString(reference ? "Consumer" : "Owner"), new Version(1, 0, 0, 0),
            default, default, 0, AssemblyHashAlgorithm.None);
        var nameHandle = metadata.GetOrAddString(name);
        var namespaceHandle = metadata.GetOrAddString(nameSpace);
        if (reference)
        {
            var scope = metadata.AddAssemblyReference(metadata.GetOrAddString("Owner"), new Version(1, 0, 0, 0), default, default, 0, default);
            metadata.AddTypeReference(scope, namespaceHandle, nameHandle);
        }
        else
        {
            metadata.AddTypeDefinition(TypeAttributes.Public, namespaceHandle, nameHandle, default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        }

        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        byte[] bytes = image.ToArray();
        using (var original = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes)))
        {
            var reader = original.GetMetadataReader();
            if (invalidUtf8 >= 0)
            {
                var serializedName = reference ? reader.GetTypeReference(MetadataTokens.TypeReferenceHandle(1)).Name :
                    reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1)).Name;
                var serializedNamespace = reference ? reader.GetTypeReference(MetadataTokens.TypeReferenceHandle(1)).Namespace :
                    reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1)).Namespace;
                int offset = reader.GetHeapMetadataOffset(HeapIndex.String) +
                    MetadataTokens.GetHeapOffset(invalidUtf8 == 0 ? serializedName : serializedNamespace);
                bytes[offset] = 0x80;
            }
            if (invalidOffset >= 0)
            {
                var table = reference ? TableIndex.TypeRef : TableIndex.TypeDef;
                Assert.Equal(reference ? 6 : 14, reader.GetTableRowSize(table));
                int offset = reader.GetTableMetadataOffset(table) + (reference ? 2 : 4) + invalidOffset * 2;
                bytes[offset] = 0xff;
                bytes[offset + 1] = 0xff;
            }
        }
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes));
    }

    private sealed class ChangingNameDecoder : MetadataStringDecoder
    {
        public bool Change;
        public bool Throw;

        public ChangingNameDecoder() : base(Encoding.UTF8) { }

        public override unsafe string GetString(byte* bytes, int byteCount)
        {
            string value = base.GetString(bytes, byteCount);
            if (value == "Widget")
            {
                if (Throw)
                {
                    throw new InvalidOperationException("Name decoding failed.");
                }
                if (Change)
                {
                    return "Changed";
                }
            }
            return value;
        }
    }
}

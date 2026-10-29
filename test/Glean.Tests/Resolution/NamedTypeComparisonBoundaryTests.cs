using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Tests.Resolution;

public sealed class NamedTypeComparisonBoundaryTests
{
    [Theory]
    [InlineData("Example", "Widget", "Owner", 1, true)]
    [InlineData("example", "Widget", "Owner", 1, false)]
    [InlineData("Example", "widget", "Owner", 1, false)]
    [InlineData("Example", "Widget", "owner", 1, true)]
    [InlineData("Example", "Widget", "Other", 1, false)]
    [InlineData("Example", "Widget", "Owner", 2, false)]
    public void NamedTypes_PreserveOrdinalNamesAndFullAssemblyScope(string nameSpace, string name, string assembly, int version, bool expected)
    {
        var definitions = Create("Owner");
        var definition = AddType(definitions, "Example", "Widget", TypeAttributes.Public);
        using var definitionProvider = Serialize(definitions);
        var references = Create("Consumer");
        var scope = references.AddAssemblyReference(references.GetOrAddString(assembly), new Version(version, 0, 0, 0), default, default, 0, default);
        var reference = references.AddTypeReference(scope, references.GetOrAddString(nameSpace), references.GetOrAddString(name));
        using var referenceProvider = Serialize(references);
        var left = new TypeDefinitionSignature(definitionProvider.GetMetadataReader(), definition);
        var right = new TypeReferenceSignature(referenceProvider.GetMetadataReader(), reference);

        Assert.Equal(expected, SignatureComparison.TypeSignaturesEquivalent(left, right));
        Assert.Equal(expected, SignatureComparison.TypeSignaturesEquivalent(right, left));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedTypes_IgnoreInnerNamespacesAndPreserveOuterNames(bool wrongOuterName)
    {
        var definitions = Create("Owner");
        var outer = AddType(definitions, "Example", "Outer", TypeAttributes.Public);
        var inner = definitions.AddTypeDefinition(TypeAttributes.NestedPublic, definitions.GetOrAddString("Unused"), definitions.GetOrAddString("Inner"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        definitions.AddNestedType(inner, outer);
        using var definitionProvider = Serialize(definitions, (TableIndex.TypeDef, MetadataTokens.GetRowNumber(inner)));
        var references = Create("Consumer");
        var scope = references.AddAssemblyReference(references.GetOrAddString("Owner"), new Version(1, 0, 0, 0), default, default, 0, default);
        var outerReference = references.AddTypeReference(scope, references.GetOrAddString("Example"), references.GetOrAddString(wrongOuterName ? "Other" : "Outer"));
        var innerReference = references.AddTypeReference(outerReference, references.GetOrAddString("Unused"), references.GetOrAddString("Inner"));
        using var referenceProvider = Serialize(references, (TableIndex.TypeRef, MetadataTokens.GetRowNumber(innerReference)));

        Assert.Equal(!wrongOuterName, SignatureComparison.TypeSignaturesEquivalent(
            new TypeDefinitionSignature(definitionProvider.GetMetadataReader(), inner),
            new TypeReferenceSignature(referenceProvider.GetMetadataReader(), innerReference)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedFlagWithoutDeclaringRow_PreservesGlobalNameComparison(bool reverse)
    {
        var metadata = Create("Owner");
        var topLevel = AddType(metadata, "", "Loose", TypeAttributes.Public);
        var missingParent = metadata.AddTypeDefinition(TypeAttributes.NestedPublic, metadata.GetOrAddString("Unused"), metadata.GetOrAddString("Loose"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        using var provider = Serialize(metadata, (TableIndex.TypeDef, MetadataTokens.GetRowNumber(missingParent)));
        var reader = provider.GetMetadataReader();
        var left = new TypeDefinitionSignature(reader, reverse ? missingParent : topLevel);
        var right = new TypeDefinitionSignature(reader, reverse ? topLevel : missingParent);

        Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NilDefinitionHandles_PreserveEmptyPathAndScopeComparison(bool differentAssembly)
    {
        using var first = Serialize(Create("Owner"));
        using var second = Serialize(Create(differentAssembly ? "Other" : "Owner"));

        Assert.Equal(!differentAssembly, SignatureComparison.TypeSignaturesEquivalent(
            new TypeDefinitionSignature(first.GetMetadataReader(), default),
            new TypeDefinitionSignature(second.GetMetadataReader(), default)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidTopLevelNamespace_ThrowsBeforeComparingOtherSignatureKind(bool reference)
    {
        var metadata = Create("Owner");
        EntityHandle handle;
        if (reference)
        {
            var scope = metadata.AddAssemblyReference(metadata.GetOrAddString("Owner"), new Version(1, 0, 0, 0), default, default, 0, default);
            handle = metadata.AddTypeReference(scope, metadata.GetOrAddString("Invalid"), metadata.GetOrAddString("Widget"));
        }
        else
        {
            handle = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Invalid"), metadata.GetOrAddString("Widget"),
                default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        }
        using var provider = Serialize(metadata, (reference ? TableIndex.TypeRef : TableIndex.TypeDef, MetadataTokens.GetRowNumber(handle)));
        var reader = provider.GetMetadataReader();
        TypeSignature signature = reference
            ? new TypeReferenceSignature(reader, (TypeReferenceHandle)handle)
            : new TypeDefinitionSignature(reader, (TypeDefinitionHandle)handle);

        Assert.Throws<BadImageFormatException>(() => SignatureComparison.TypeSignaturesEquivalent(signature, PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32)));
    }

    private static MetadataBuilder Create(string name)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("sample"), default, default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        return metadata;
    }

    private static TypeDefinitionHandle AddType(MetadataBuilder metadata, string nameSpace, string name, TypeAttributes attributes)
        => metadata.AddTypeDefinition(attributes, metadata.GetOrAddString(nameSpace), metadata.GetOrAddString(name), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata, params (TableIndex Table, int Row)[] invalidNamespaces)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        byte[] bytes = image.ToArray();
        if (invalidNamespaces.Length > 0)
        {
            using var valid = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes));
            var reader = valid.GetMetadataReader();
            foreach (var entry in invalidNamespaces)
            {
                Assert.Equal(entry.Table == TableIndex.TypeDef ? 14 : 6, reader.GetTableRowSize(entry.Table));
                int offset = reader.GetTableMetadataOffset(entry.Table) + (entry.Row - 1) * reader.GetTableRowSize(entry.Table) +
                    (entry.Table == TableIndex.TypeDef ? 6 : 4);
                bytes[offset] = 0xff;
                bytes[offset + 1] = 0xff;
            }
        }
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes));
    }
}

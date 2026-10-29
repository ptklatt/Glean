using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using Xunit;

using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Tests.Resolution;

public sealed class NamedTypeCustomDecoderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredNamedTypes_PreserveCustomCultureDecoding(bool throws)
    {
        var metadata = Create();
        metadata.AddAssembly(metadata.GetOrAddString("Owner"), new Version(1, 0, 0, 0), metadata.GetOrAddString("fr-FR"), default, 0, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Example"), metadata.GetOrAddString("Widget"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        using var definition = Serialize(metadata);
        var decoder = new CultureDecoder { ThrowOnCulture = throws };
        var reader = definition.GetMetadataReader(MetadataReaderOptions.Default, decoder);
        using var set = new AssemblySet();
        set.Add(reader);
        decoder.Changed = true;

        var references = Create();
        references.AddAssembly(references.GetOrAddString("Consumer"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        var scope = references.AddAssemblyReference(references.GetOrAddString("Owner"), new Version(1, 0, 0, 0),
            references.GetOrAddString("en-US"), default, 0, default);
        var handle = references.AddTypeReference(scope, references.GetOrAddString("Example"), references.GetOrAddString("Widget"));
        using var reference = Serialize(references);
        var left = new TypeDefinitionSignature(reader, MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(reference.GetMetadataReader(), handle);
        if (throws)
        {
            Assert.Throws<InvalidOperationException>(() => SignatureComparison.TypeSignaturesEquivalent(left, right, set));
        }
        else
        {
            Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right, set));
        }
    }

    private static MetadataBuilder Create()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("sample"), default, default, default);
        return metadata;
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }

    private sealed class CultureDecoder() : MetadataStringDecoder(Encoding.UTF8)
    {
        public bool Changed { get; set; }
        public bool ThrowOnCulture { get; set; }

        public override unsafe string GetString(byte* bytes, int byteCount)
        {
            string value = base.GetString(bytes, byteCount);
            if ((value == "fr-FR") && Changed)
            {
                if (ThrowOnCulture)
                {
                    throw new InvalidOperationException("Culture decoding failed.");
                }
                return "en-US";
            }
            return value;
        }
    }
}

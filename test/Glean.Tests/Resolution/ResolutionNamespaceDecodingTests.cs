using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using Xunit;

using Glean.Resolution;

namespace Glean.Tests.Resolution;

public sealed class ResolutionNamespaceDecodingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Lookup_CustomDecoder_PreservesRepeatedNamespaceDecoding(bool forwarders, bool throws)
    {
        using var provider = Create(forwarders, nilNamespace: false);
        var decoder = new ChangingDecoder { ThrowOnRepeatedRead = throws };
        var reader = provider.GetMetadataReader(MetadataReaderOptions.Default, decoder);

        if (throws)
        {
            Assert.Throws<InvalidOperationException>(() => Read(reader, forwarders));
            Assert.Equal(2, decoder.NamespaceCalls);
            decoder.ThrowOnRepeatedRead = false;
            decoder.NamespaceCalls = 0;
        }

        var lookup = Read(reader, forwarders);
        Assert.Equal(2, lookup.Count);
        Assert.Equal(1, lookup[("First", "Widget")]);
        Assert.Equal(2, lookup[("Second", "Widget")]);
        Assert.Equal(2, decoder.NamespaceCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Lookup_NilNamespaces_PreservesLastDuplicate(bool forwarders)
    {
        using var provider = Create(forwarders, nilNamespace: true);
        var lookup = Read(provider.GetMetadataReader(), forwarders);

        Assert.Single(lookup);
        Assert.Equal(2, lookup[(string.Empty, "Widget")]);
    }

    private static Dictionary<(string, string), int> Read(MetadataReader reader, bool forwarders)
        => forwarders
            ? ResolutionCore.BuildForwarderLookup(reader).ToDictionary(p => p.Key, p => MetadataTokens.GetRowNumber(p.Value))
            : ResolutionCore.BuildTypeLookup(reader).ToDictionary(p => p.Key, p => MetadataTokens.GetRowNumber(p.Value));

    private static MetadataReaderProvider Create(bool forwarders, bool nilNamespace)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("namespaces"), default, default, default);
        var ns = nilNamespace ? default : metadata.GetOrAddString("Repeated");
        var name = metadata.GetOrAddString("Widget");
        var assembly = metadata.AddAssemblyReference(metadata.GetOrAddString("Dependency"), new Version(1, 0, 0, 0),
            default, default, 0, default);
        for (int i = 0; i < 2; i++)
        {
            if (forwarders)
            {
                metadata.AddExportedType(TypeAttributes.Public, ns, name, assembly, 0);
            }
            else
            {
                metadata.AddTypeDefinition(TypeAttributes.Public, ns, name, default,
                    MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            }
        }

        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }

    private sealed class ChangingDecoder() : MetadataStringDecoder(Encoding.UTF8)
    {
        public int NamespaceCalls { get; set; }
        public bool ThrowOnRepeatedRead { get; set; }

        public override unsafe string GetString(byte* bytes, int byteCount)
        {
            string value = base.GetString(bytes, byteCount);
            if (value == "Repeated")
            {
                NamespaceCalls++;
                if (ThrowOnRepeatedRead && (NamespaceCalls == 2))
                {
                    throw new InvalidOperationException("Repeated namespace decoding failed.");
                }
                return NamespaceCalls == 1 ? "First" : "Second";
            }
            return value;
        }
    }
}

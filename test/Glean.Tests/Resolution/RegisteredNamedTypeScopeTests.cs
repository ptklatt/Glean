using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;

using Xunit;

using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Tests.Resolution;

public sealed class RegisteredNamedTypeScopeTests
{
    [Theory]
    [InlineData("unregistered", false)]
    [InlineData("unregistered", true)]
    [InlineData("registered", false)]
    [InlineData("registered", true)]
    [InlineData("different-reader", false)]
    [InlineData("different-reader", true)]
    [InlineData("multiple-versions", false)]
    [InlineData("multiple-versions", true)]
    [InlineData("replaced-reader", false)]
    [InlineData("replaced-reader", true)]
    public void SignedNamedTypes_PreserveIdentityAcrossRegistrationStates(string state, bool fullReferenceKey)
    {
        byte[] key = typeof(object).Assembly.GetName().GetPublicKey()!;
        using var definition = CreateDefinition("Owner", 1, "fr-FR", key);
        using var otherVersion = CreateDefinition("Owner", 2, "fr-FR", key);
        using var replacement = CreateDefinition("OWNER", 1, "FR-fr", key);
        using var reference = CreateReference("owner", 1, "FR-fr", key, fullReferenceKey);
        var reader = definition.GetMetadataReader();
        using var set = new AssemblySet();
        switch (state)
        {
            case "registered":
                set.Add(reader);
                break;
            case "different-reader":
                set.Add(otherVersion.GetMetadataReader());
                break;
            case "multiple-versions":
                set.Add(reader);
                set.Add(otherVersion.GetMetadataReader());
                break;
            case "replaced-reader":
                set.Add(reader);
                set.Add(replacement.GetMetadataReader());
                break;
        }

        var left = new TypeDefinitionSignature(reader, MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(reference.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right, set));
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(right, left, set));
        set.ClearCaches();
        Assert.True(SignatureComparison.TypeSignaturesEquivalent(left, right, set));
    }

    [Theory]
    [InlineData("name", false)]
    [InlineData("name", true)]
    [InlineData("version", false)]
    [InlineData("version", true)]
    [InlineData("culture", false)]
    [InlineData("culture", true)]
    [InlineData("key", false)]
    [InlineData("key", true)]
    [InlineData("unsigned", false)]
    [InlineData("unsigned", true)]
    public void RegisteredNamedTypes_RejectFullIdentityMismatches(string mismatch, bool fullReferenceKey)
    {
        byte[] key = typeof(object).Assembly.GetName().GetPublicKey()!;
        using var definition = CreateDefinition("Owner", 1, "fr-FR", key);
        byte[] referenceKey = (byte[])key.Clone();
        if (mismatch == "key")
        {
            referenceKey[^1] ^= 1;
        }
        if (mismatch == "unsigned")
        {
            referenceKey = [];
        }
        using var reference = CreateReference(mismatch == "name" ? "Other" : "Owner", mismatch == "version" ? 2 : 1,
            mismatch == "culture" ? "en-US" : "fr-FR", referenceKey, fullReferenceKey);
        var reader = definition.GetMetadataReader();
        using var set = new AssemblySet();
        set.Add(reader);

        var left = new TypeDefinitionSignature(reader, MetadataTokens.TypeDefinitionHandle(1));
        var right = new TypeReferenceSignature(reference.GetMetadataReader(), MetadataTokens.TypeReferenceHandle(1));
        Assert.False(SignatureComparison.TypeSignaturesEquivalent(left, right, set));
        Assert.False(SignatureComparison.TypeSignaturesEquivalent(right, left, set));
    }

    private static MetadataReaderProvider CreateDefinition(string name, int version, string culture, byte[] key)
    {
        var metadata = Create();
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(version, 0, 0, 0), metadata.GetOrAddString(culture),
            metadata.GetOrAddBlob(key), AssemblyFlags.PublicKey, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Example"), metadata.GetOrAddString("Widget"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        return Serialize(metadata);
    }

    private static MetadataReaderProvider CreateReference(string name, int version, string culture, byte[] key, bool fullKey)
    {
        var metadata = Create();
        metadata.AddAssembly(metadata.GetOrAddString("Consumer"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
        byte[] token = key.Length == 0 ? [] : fullKey ? key : SHA1.HashData(key)[^8..].Reverse().ToArray();
        var scope = metadata.AddAssemblyReference(metadata.GetOrAddString(name), new Version(version, 0, 0, 0),
            metadata.GetOrAddString(culture), metadata.GetOrAddBlob(token), fullKey ? AssemblyFlags.PublicKey : 0, default);
        metadata.AddTypeReference(scope, metadata.GetOrAddString("Example"), metadata.GetOrAddString("Widget"));
        return Serialize(metadata);
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
}

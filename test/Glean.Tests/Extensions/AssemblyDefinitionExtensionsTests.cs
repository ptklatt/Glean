using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class AssemblyDefinitionExtensionsTests
{
    private const string ReferenceAssemblySource = """
        using System.Runtime.CompilerServices;

        [assembly: ReferenceAssembly]

        namespace Glean.Tests.Extensions;

        internal static class ReferenceAssemblyFixture
        {
        }
        """;

    // == Flag checks =========================================================

    [Fact]
    public void FlagChecks_SyntheticAssembly_ReportExpectedValues()
    {
        using var provider = CreateAssemblyMetadataReaderProvider(
            "Synthetic.Assembly",
            "de-DE",
            new byte[] { 1, 2, 3, 4 },
            AssemblyFlags.PublicKey |
            AssemblyFlags.Retargetable |
            AssemblyFlags.DisableJitCompileOptimizer |
            AssemblyFlags.EnableJitCompileTracking);
        var reader = provider.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();

        Assert.True(assembly.HasPublicKey());
        Assert.True(assembly.IsRetargetable());
        Assert.True(assembly.IsJitCompileOptimizerDisabled());
        Assert.True(assembly.IsJitCompileTrackingEnabled());
        Assert.True(assembly.IsDefaultContentType());
        Assert.False(assembly.IsWindowsRuntime());
    }

    [Fact]
    public void FlagChecks_WindowsRuntimeAssembly_ReportExpectedValues()
    {
        using var provider = CreateAssemblyMetadataReaderProvider(
            "WindowsRuntime.Assembly",
            null,
            null,
            (AssemblyFlags)0x00000200);
        var reader = provider.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();

        Assert.False(assembly.HasPublicKey());
        Assert.False(assembly.IsDefaultContentType());
        Assert.True(assembly.IsWindowsRuntime());
    }

    [Fact]
    public void IsReferenceAssembly_ReferenceAssemblyAttribute_ReturnsTrue()
    {
        using var metadata = TestUtility.BuildMetadata(ReferenceAssemblySource);
        var assembly = metadata.Reader.GetAssemblyDefinition();

        Assert.True(assembly.IsReferenceAssembly(metadata.Reader));
    }

    // == Metadata access =====================================================

    [Fact]
    public void MetadataAccess_SyntheticAssembly_ReturnExpectedValues()
    {
        byte[] publicKey = [10, 20, 30, 40];
        using var provider = CreateAssemblyMetadataReaderProvider(
            "Synthetic.Assembly",
            "de-DE",
            publicKey,
            AssemblyFlags.PublicKey);
        var reader = provider.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();

        Assert.Equal("Synthetic.Assembly", assembly.GetName(reader));
        Assert.Equal("de-DE", assembly.GetCulture(reader));
        Assert.True(assembly.NameIs(reader, "Synthetic.Assembly"));
        Assert.False(assembly.NameIs(reader, "Other.Assembly"));
        Assert.Equal(publicKey, assembly.GetPublicKey(reader));
        Assert.True(assembly.GetPublicKeySpan(reader).SequenceEqual(publicKey));
    }

    [Fact]
    public void MetadataAccess_AssemblyWithoutCultureOrPublicKey_ReturnExpectedDefaults()
    {
        using var provider = CreateAssemblyMetadataReaderProvider(
            "Synthetic.Assembly",
            null,
            null,
            0);
        var reader = provider.GetMetadataReader();
        var assembly = reader.GetAssemblyDefinition();

        Assert.Equal(string.Empty, assembly.GetCulture(reader));
        Assert.Empty(assembly.GetPublicKey(reader));
        Assert.True(assembly.GetPublicKeySpan(reader).IsEmpty);
    }

    private static MetadataReaderProvider CreateAssemblyMetadataReaderProvider(
        string assemblyName,
        string? culture,
        byte[]? publicKey,
        AssemblyFlags flags)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString($"{assemblyName}.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString(assemblyName),
            new Version(1, 2, 3, 4),
            culture is null ? default : metadata.GetOrAddString(culture),
            publicKey is null ? default : metadata.GetOrAddBlob(publicKey),
            flags,
            AssemblyHashAlgorithm.Sha256);
        metadata.AddTypeDefinition(
            0,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var root = new MetadataRootBuilder(metadata);
        var image = new BlobBuilder();
        root.Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }
}

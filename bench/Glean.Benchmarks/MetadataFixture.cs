using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Glean.Benchmarks;

internal sealed class MetadataFixture : IDisposable
{
    public const int TypeCount = 128;
    public const int MethodsPerType = 8;
    public const string TargetNamespace = "Fixture.Selected";
    private readonly MetadataReaderProvider _definitions;
    private readonly MetadataReaderProvider _references;

    public MetadataFixture()
    {
        _definitions = BuildDefinitions();
        _references = BuildReferences();
        Reader = _definitions.GetMetadataReader();
        Source = _references.GetMetadataReader();
        TypeReferences = Source.TypeReferences.ToArray();
        MemberReferences = Source.MemberReferences.ToArray();
        Require(Reader.TypeDefinitions.Count == TypeCount + 1, "Type fixture count");
        Require(Reader.MethodDefinitions.Count == TypeCount * MethodsPerType, "Method fixture count");
        Require(Reader.PropertyDefinitions.Count == TypeCount * 2, "Property fixture count");
        Require(TypeReferences.Length == TypeCount, "Type reference count");
        Require(MemberReferences.Length == TypeCount * 2, "Member reference count");
    }

    public MetadataReader Reader { get; }
    public MetadataReader Source { get; }
    public TypeReferenceHandle[] TypeReferences { get; }
    public MemberReferenceHandle[] MemberReferences { get; }

    public void Dispose()
    {
        _references.Dispose();
        _definitions.Dispose();
    }

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static MetadataBuilder CreateMetadata(string name)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(name + ".dll"),
            metadata.GetOrAddGuid(new Guid("43d4e731-920a-42ac-b796-dc4d748ef84a")), default, default);
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(1, 2, 3, 4),
            default, default, (AssemblyFlags)0, AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static MetadataReaderProvider BuildDefinitions()
    {
        var metadata = CreateMetadata("Benchmark.Definitions");
        var intSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x01, 0x08, 0x08 });
        var longSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x01, 0x0a, 0x0a });
        var richSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x02, 0x1d, 0x08, 0x1d, 0x08, 0x0e });
        var propertySignature = metadata.GetOrAddBlob(new byte[] { 0x08, 0x00, 0x08 });
        var indexerSignature = metadata.GetOrAddBlob(new byte[] { 0x08, 0x01, 0x08, 0x08 });

        for (int i = 0; i < TypeCount; i++)
        {
            var type = metadata.AddTypeDefinition(i % 2 == 0 ? TypeAttributes.Public : TypeAttributes.NotPublic,
                metadata.GetOrAddString(i % 2 == 0 ? TargetNamespace : "Fixture.Other"),
                metadata.GetOrAddString($"Type{i:D3}"), default,
                MetadataTokens.FieldDefinitionHandle(1),
                MetadataTokens.MethodDefinitionHandle(i * MethodsPerType + 1));
            for (int j = 0; j < MethodsPerType; j++)
            {
                metadata.AddMethodDefinition(
                    (j % 2 == 0 ? MethodAttributes.Public : MethodAttributes.Private) | MethodAttributes.Static,
                    MethodImplAttributes.Runtime, metadata.GetOrAddString(j < 2 ? "Echo" : $"Transform{j}"),
                    j == 0 ? intSignature : j == 1 ? longSignature : richSignature,
                    0, MetadataTokens.ParameterHandle(1));
            }

            var firstProperty = metadata.AddProperty(PropertyAttributes.None,
                metadata.GetOrAddString("Value"), propertySignature);
            metadata.AddProperty(PropertyAttributes.None, metadata.GetOrAddString("Item"), indexerSignature);
            metadata.AddPropertyMap(type, firstProperty);
        }

        return Serialize(metadata);
    }

    private static MetadataReaderProvider BuildReferences()
    {
        var metadata = CreateMetadata("Benchmark.References");
        var assembly = metadata.AddAssemblyReference(metadata.GetOrAddString("Benchmark.Definitions"),
            new Version(1, 2, 3, 4), default, default, (AssemblyFlags)0, default);
        var intSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x01, 0x08, 0x08 });
        var longSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x01, 0x0a, 0x0a });
        for (int i = 0; i < TypeCount; i++)
        {
            var type = metadata.AddTypeReference(assembly,
                metadata.GetOrAddString(i % 2 == 0 ? TargetNamespace : "Fixture.Other"),
                metadata.GetOrAddString($"Type{i:D3}"));
            metadata.AddMemberReference(type, metadata.GetOrAddString("Echo"), intSignature);
            metadata.AddMemberReference(type, metadata.GetOrAddString("Echo"), longSignature);
        }

        return Serialize(metadata);
    }

    private static MetadataReaderProvider Serialize(MetadataBuilder metadata)
    {
        var blob = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(blob, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(blob.ToArray()));
    }
}

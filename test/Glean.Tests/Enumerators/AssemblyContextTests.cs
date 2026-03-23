using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Contexts;
using Glean.Tests.Utility;

namespace Glean.Tests.Enumerators;

public class AssemblyContextTests
{
    private const TypeAttributes ForwarderTypeAttribute = (TypeAttributes)0x00200000;

    private const string AssemblyContextSource = """
        using System;
        using System.Reflection;

        [assembly: AssemblyVersion("1.2.3.4")]
        [assembly: Glean.Tests.Enumerators.AssemblyMarker]
        [assembly: Glean.Tests.Enumerators.AssemblyNoise]

        namespace Glean.Tests.Enumerators;

        [AttributeUsage(AttributeTargets.Assembly)]
        internal sealed class AssemblyMarkerAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Assembly)]
        internal sealed class AssemblyNoiseAttribute : Attribute
        {
        }

        internal sealed class AssemblyContextFixture
        {
            public static void TouchConsole()
            {
                Console.WriteLine(string.Empty);
            }
        }
        """;

    // == Create ===============================================================

    [Fact]
    public void Create_ModuleMetadata_ThrowsArgumentException()
    {
        using var provider = CreateModuleOnlyMetadataReaderProvider();
        var reader = provider.GetMetadataReader();

        Assert.Throws<ArgumentException>(() => AssemblyContext.Create(reader));
    }

    // == Metadata Access ======================================================

    [Fact]
    public void MetadataAccess_CompiledAssembly_ReturnsExpectedIdentity()
    {
        using var metadata = CreateAssemblyContextMetadataScope();
        var definition = metadata.Reader.GetAssemblyDefinition();
        var assembly = AssemblyContext.Create(metadata.Reader);

        Assert.True(assembly.IsValid);
        Assert.Equal("AssemblyContextFixture", assembly.Name);
        Assert.Equal(string.Empty, assembly.Culture);
        Assert.Equal(new Version(1, 2, 3, 4), assembly.Version);
        Assert.Equal(definition.HashAlgorithm, assembly.HashAlgorithm);
        Assert.True(assembly.PublicKeyHandle.IsNil);
    }

    [Fact]
    public void MetadataAccess_CompiledAssembly_EnumeratesCustomAttributes()
    {
        using var metadata = CreateAssemblyContextMetadataScope();
        var assembly = AssemblyContext.Create(metadata.Reader);

        var attributeNames = new List<string>();
        var allAttributes = assembly.EnumerateCustomAttributes();
        while (allAttributes.MoveNext())
        {
            attributeNames.Add(GetAttributeTypeName(allAttributes.Current));
        }

        Assert.Contains("AssemblyMarkerAttribute", attributeNames);
        Assert.Contains("AssemblyNoiseAttribute", attributeNames);

        var filtered = assembly.EnumerateAttributes("Glean.Tests.Enumerators", "AssemblyMarkerAttribute");
        Assert.True(filtered.MoveNext());
        Assert.Equal("AssemblyMarkerAttribute", GetAttributeTypeName(filtered.Current));
        Assert.False(filtered.MoveNext());

        Assert.True(assembly.HasAttribute("Glean.Tests.Enumerators", "AssemblyMarkerAttribute"));
        Assert.True(assembly.TryFindAttribute("Glean.Tests.Enumerators", "AssemblyMarkerAttribute", out var attribute));
        Assert.Equal("AssemblyMarkerAttribute", GetAttributeTypeName(attribute));
    }

    [Fact]
    public void MetadataAccess_CompiledAssembly_EnumeratesManifestResources()
    {
        using var metadata = CreateAssemblyContextMetadataScope();
        var assembly = AssemblyContext.Create(metadata.Reader);

        var enumerator = assembly.EnumerateManifestResources();
        var resources = new List<(string Name, ManifestResourceAttributes Attributes, EntityHandle Implementation)>();
        while (enumerator.MoveNext())
        {
            var resource = enumerator.Current;
            resources.Add((resource.Name, resource.Attributes, resource.Implementation));
        }

        Assert.Equal(2, resources.Count);
        Assert.Equal("PublicResource.bin", resources[0].Name);
        Assert.Equal(ManifestResourceAttributes.Public, resources[0].Attributes);
        Assert.True(resources[0].Implementation.IsNil);

        Assert.Equal("PrivateResource.bin", resources[1].Name);
        Assert.Equal(ManifestResourceAttributes.Private, resources[1].Attributes);
        Assert.True(resources[1].Implementation.IsNil);
    }

    [Fact]
    public void MetadataAccess_SyntheticAssembly_EnumeratesManifestResourceImplementationAndOffset()
    {
        using var provider = CreateManifestResourceMetadataReaderProvider();
        var assembly = AssemblyContext.Create(provider.GetMetadataReader());

        var enumerator = assembly.EnumerateManifestResources();

        Assert.True(enumerator.MoveNext());
        var embedded = enumerator.Current;
        Assert.Equal("EmbeddedResource.bin", embedded.Name);
        Assert.Equal(ManifestResourceAttributes.Public, embedded.Attributes);
        Assert.Equal(0L, embedded.Offset);
        Assert.True(embedded.Implementation.IsNil);

        Assert.True(enumerator.MoveNext());
        var linked = enumerator.Current;
        Assert.Equal("LinkedResource.bin", linked.Name);
        Assert.Equal(ManifestResourceAttributes.Private, linked.Attributes);
        Assert.Equal(128L, linked.Offset);
        Assert.Equal(HandleKind.AssemblyReference, linked.Implementation.Kind);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MetadataAccess_CompiledAssembly_EnumeratesTypesAndAssemblyReferences()
    {
        using var metadata = CreateAssemblyContextMetadataScope();
        var assembly = AssemblyContext.Create(metadata.Reader);

        var typeNames = new List<string>();
        var typeEnumerator = assembly.EnumerateTypes();
        while (typeEnumerator.MoveNext())
        {
            typeNames.Add(typeEnumerator.Current.Name);
        }

        Assert.Contains("AssemblyContextFixture", typeNames);

        var expectedReferences = new List<(string Name, Version Version)>();
        foreach (var handle in metadata.Reader.AssemblyReferences)
        {
            var reference = metadata.Reader.GetAssemblyReference(handle);
            expectedReferences.Add((
                metadata.Reader.GetString(reference.Name),
                reference.Version));
        }

        var actualReferences = new List<(string Name, Version Version)>();
        var referenceEnumerator = assembly.EnumerateAssemblyReferences();
        while (referenceEnumerator.MoveNext())
        {
            actualReferences.Add((
                referenceEnumerator.Current.Name,
                referenceEnumerator.Current.Version));
        }

        Assert.Equal(expectedReferences, actualReferences);
    }

    [Fact]
    public void MetadataAccess_CompiledAssembly_FindsTypeByNamespaceAndName()
    {
        using var metadata = CreateAssemblyContextMetadataScope();
        var assembly = AssemblyContext.Create(metadata.Reader);

        Assert.True(assembly.TryFindType("Glean.Tests.Enumerators", "AssemblyContextFixture", out var type));
        Assert.True(type.IsValid);
        Assert.Equal("AssemblyContextFixture", type.Name);

        Assert.False(assembly.TryFindType("Glean.Tests.Enumerators", "MissingType", out var missing));
        Assert.Equal(default, missing);
    }

    [Fact]
    public void MetadataAccess_SyntheticAssembly_EnumeratesExportedTypes()
    {
        using var provider = CreateExportedTypeMetadataReaderProvider();
        var assembly = AssemblyContext.Create(provider.GetMetadataReader());

        var enumerator = assembly.EnumerateExportedTypes();

        Assert.True(enumerator.MoveNext());
        var first = enumerator.Current;
        Assert.Equal("Glean.Tests.Forwarded", first.Namespace);
        Assert.Equal("ForwardedType", first.Name);
        Assert.Equal(HandleKind.AssemblyReference, first.Implementation.Kind);
        Assert.True(first.IsForwarder);
        Assert.True((first.Attributes & ForwarderTypeAttribute) != 0);

        Assert.True(enumerator.MoveNext());
        var second = enumerator.Current;
        Assert.Equal(string.Empty, second.Namespace);
        Assert.Equal("NestedForwardedType", second.Name);
        Assert.Equal(HandleKind.ExportedType, second.Implementation.Kind);
        Assert.False(second.IsForwarder);
        Assert.Equal(TypeAttributes.NestedPublic, second.Attributes);

        Assert.False(enumerator.MoveNext());
    }

    private static MetadataScope CreateAssemblyContextMetadataScope()
    {
        var builder = new TestAssemblyBuilder("AssemblyContextFixture")
            .WithSource(AssemblyContextSource)
            .WithManifestResource("PublicResource.bin", [1, 2, 3], isPublic: true)
            .WithManifestResource("PrivateResource.bin", [4, 5], isPublic: false);

        return builder.BuildMetadataScope();
    }

    private static MetadataReaderProvider CreateModuleOnlyMetadataReaderProvider()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("ModuleOnly.netmodule"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
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

    private static MetadataReaderProvider CreateExportedTypeMetadataReaderProvider()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticExportedTypes.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("SyntheticExportedTypes"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);

        var assemblyReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("Forwarded.Library"),
            new Version(2, 0, 0, 0),
            default,
            default,
            0,
            default);
        var forwardedType = metadata.AddExportedType(
            TypeAttributes.Public | ForwarderTypeAttribute,
            metadata.GetOrAddString("Glean.Tests.Forwarded"),
            metadata.GetOrAddString("ForwardedType"),
            assemblyReference,
            1);
        metadata.AddExportedType(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("NestedForwardedType"),
            forwardedType,
            2);
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

    private static MetadataReaderProvider CreateManifestResourceMetadataReaderProvider()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticResources.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("SyntheticResources"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);

        var assemblyReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("External.Resources"),
            new Version(3, 0, 0, 0),
            default,
            default,
            0,
            default);
        metadata.AddManifestResource(
            ManifestResourceAttributes.Public,
            metadata.GetOrAddString("EmbeddedResource.bin"),
            default,
            0);
        metadata.AddManifestResource(
            ManifestResourceAttributes.Private,
            metadata.GetOrAddString("LinkedResource.bin"),
            assemblyReference,
            128);
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

    private static string GetAttributeTypeName(CustomAttributeContext attribute)
    {
        if (attribute.TryGetConstructorDefinition(out var constructor))
        {
            return constructor.DeclaringType.Name;
        }

        if (attribute.TryGetConstructorReference(out var memberReference))
        {
            var parent = memberReference.Parent;
            if (parent.Kind == HandleKind.TypeReference)
            {
                return attribute.Reader.GetString(attribute.Reader.GetTypeReference((TypeReferenceHandle)parent).Name);
            }

            if (parent.Kind == HandleKind.TypeDefinition)
            {
                return attribute.Reader.GetString(attribute.Reader.GetTypeDefinition((TypeDefinitionHandle)parent).Name);
            }
        }

        throw new InvalidOperationException("Could not resolve custom attribute type name.");
    }
}

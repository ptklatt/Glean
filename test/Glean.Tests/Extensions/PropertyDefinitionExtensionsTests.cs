using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class PropertyDefinitionExtensionsTests
{
    private const string PropertySource = """
        namespace Glean.Tests.Extensions;

        internal sealed class PropertyFixture
        {
            private int _writeOnlyBackingField;

            internal int ReadOnlyProperty { get; } = 1;

            internal int ReadWriteProperty { get; set; }

            internal int WriteOnlyProperty
            {
                set
                {
                    _writeOnlyBackingField = value;
                }
            }
        }
        """;

    // == Accessors ===========================================================

    [Fact]
    public void AccessorHelpers_CompiledProperties_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(PropertySource);

        var readOnlyProperty = GetPropertyDefinition(metadata, "Glean.Tests.Extensions", "PropertyFixture", "ReadOnlyProperty");
        var readWriteProperty = GetPropertyDefinition(metadata, "Glean.Tests.Extensions", "PropertyFixture", "ReadWriteProperty");
        var writeOnlyProperty = GetPropertyDefinition(metadata, "Glean.Tests.Extensions", "PropertyFixture", "WriteOnlyProperty");

        Assert.True(readOnlyProperty.HasGetter());
        Assert.False(readOnlyProperty.HasSetter());
        Assert.True(readOnlyProperty.IsReadOnly());
        Assert.False(readOnlyProperty.IsWriteOnly());
        Assert.True(readOnlyProperty.NameIs(metadata.Reader, "ReadOnlyProperty"));
        Assert.Equal("get_ReadOnlyProperty", GetMethodName(metadata.Reader, readOnlyProperty.GetGetter()));
        Assert.True(readOnlyProperty.GetSetter().IsNil);

        Assert.True(readWriteProperty.HasGetter());
        Assert.True(readWriteProperty.HasSetter());
        Assert.False(readWriteProperty.IsReadOnly());
        Assert.False(readWriteProperty.IsWriteOnly());
        Assert.Equal("get_ReadWriteProperty", GetMethodName(metadata.Reader, readWriteProperty.GetGetter()));
        Assert.Equal("set_ReadWriteProperty", GetMethodName(metadata.Reader, readWriteProperty.GetSetter()));

        Assert.False(writeOnlyProperty.HasGetter());
        Assert.True(writeOnlyProperty.HasSetter());
        Assert.False(writeOnlyProperty.IsReadOnly());
        Assert.True(writeOnlyProperty.IsWriteOnly());
        Assert.True(writeOnlyProperty.GetGetter().IsNil);
        Assert.Equal("set_WriteOnlyProperty", GetMethodName(metadata.Reader, writeOnlyProperty.GetSetter()));
    }

    // == Flags and defaults ==================================================

    [Fact]
    public void FlagAndDefaultHelpers_SyntheticProperty_ReportExpectedValues()
    {
        using var provider = CreatePropertyMetadataReaderProvider(
            "SyntheticProperty",
            PropertyAttributes.SpecialName | PropertyAttributes.RTSpecialName | PropertyAttributes.HasDefault,
            42);
        var reader = provider.GetMetadataReader();
        var property = reader.GetPropertyDefinition(MetadataTokens.PropertyDefinitionHandle(1));

        Assert.True(property.IsSpecialName());
        Assert.True(property.IsRTSpecialName());
        Assert.True(property.HasDefault());
        Assert.Equal(42, property.GetDefaultValue(reader));

        Assert.True(property.TryGetDefaultValue<int>(reader, out int value));
        Assert.Equal(42, value);

        Assert.False(property.TryGetDefaultValue<bool>(reader, out bool wrongType));
        Assert.False(wrongType);
    }

    [Fact]
    public void DefaultHelpers_PropertyWithoutConstant_ReturnExpectedDefaults()
    {
        using var provider = CreatePropertyMetadataReaderProvider(
            "PlainProperty",
            0,
            null);
        var reader = provider.GetMetadataReader();
        var property = reader.GetPropertyDefinition(MetadataTokens.PropertyDefinitionHandle(1));

        Assert.False(property.IsSpecialName());
        Assert.False(property.IsRTSpecialName());
        Assert.False(property.HasDefault());
        Assert.Null(property.GetDefaultValue(reader));

        Assert.False(property.TryGetDefaultValue<int>(reader, out int value));
        Assert.Equal(0, value);
    }

    private static PropertyDefinition GetPropertyDefinition(
        MetadataScope metadata,
        string ns,
        string typeName,
        string propertyName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        foreach (var handle in typeDefinition.GetProperties())
        {
            var propertyDefinition = metadata.Reader.GetPropertyDefinition(handle);
            if (propertyDefinition.NameIs(metadata.Reader, propertyName))
            {
                return propertyDefinition;
            }
        }

        throw new InvalidOperationException($"Could not locate property {typeName}.{propertyName}.");
    }

    private static string GetMethodName(MetadataReader reader, MethodDefinitionHandle handle)
    {
        return reader.GetString(reader.GetMethodDefinition(handle).Name);
    }

    private static MetadataReaderProvider CreatePropertyMetadataReaderProvider(
        string propertyName,
        PropertyAttributes attributes,
        object? defaultValue)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("Synthetic.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("Synthetic"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            0);
        metadata.AddTypeDefinition(
            0,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        var typeHandle = metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Glean.Tests.Extensions"),
            metadata.GetOrAddString("SyntheticPropertyFixture"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var propertyHandle = metadata.AddProperty(
            attributes,
            metadata.GetOrAddString(propertyName),
            metadata.GetOrAddBlob(CreateInt32PropertySignature()));
        metadata.AddPropertyMap(typeHandle, propertyHandle);

        if (defaultValue is not null)
        {
            metadata.AddConstant(propertyHandle, defaultValue);
        }

        var root = new MetadataRootBuilder(metadata);
        var image = new BlobBuilder();
        root.Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }

    private static BlobBuilder CreateInt32PropertySignature()
    {
        var blob = new BlobBuilder();
        new BlobEncoder(blob)
            .PropertySignature(isInstanceProperty: true)
            .Parameters(
                parameterCount: 0,
                returnType => returnType.Type().Int32(),
                parameters => { });
        return blob;
    }
}

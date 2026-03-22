using System.Reflection.Metadata;

using Xunit;

using Glean.Contexts;
using Glean.Extensions;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class AccessorSignatureExtensionsTests
{
    private const string AccessorSignatureSource = """
        using System;

        namespace Glean.Tests.Extensions;

        internal sealed class CustomEventArgs : EventArgs
        {
        }

        internal sealed class AccessorSignatureFixture
        {
            internal event EventHandler? Changed;
            internal event EventHandler<CustomEventArgs>? GenericChanged;

            internal int Number { get; set; }

            internal string this[int index] => index.ToString();
        }
        """;

    // == Event Handler Types ==================================================

    [Fact]
    public void GetEventHandlerType_EventDefinitionAndContext_ReturnExpectedSignatures()
    {
        using var metadata = TestUtility.BuildMetadata(AccessorSignatureSource);
        var reader = metadata.Reader;
        var changedEvent = GetEventDefinition(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "Changed");
        var genericChangedEvent = GetEventDefinition(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "GenericChanged");
        var genericChangedContext = EventContext.Create(
            reader,
            GetEventHandle(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "GenericChanged"));

        var changedType = changedEvent.GetEventHandlerType(reader);
        var changedReference = Assert.IsType<TypeReferenceSignature>(changedType);
        Assert.True(changedReference.Is("System", "EventHandler"));

        var genericChangedType = genericChangedContext.GetEventHandlerType();
        var genericInstance = Assert.IsType<GenericInstanceSignature>(genericChangedType);
        Assert.True(genericInstance.Is("System", "EventHandler`1"));

        var argument = Assert.IsType<TypeDefinitionSignature>(Assert.Single(genericInstance.Arguments));
        Assert.True(argument.Is("Glean.Tests.Extensions", "CustomEventArgs"));
    }

    // == Property Types =======================================================

    [Fact]
    public void GetPropertyType_CompiledProperties_ReturnExpectedTypes()
    {
        using var metadata = TestUtility.BuildMetadata(AccessorSignatureSource);
        var numberProperty = GetPropertyDefinition(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "Number");
        var indexerHandle = GetPropertyHandle(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "Item");
        var indexerContext = PropertyContext.Create(metadata.Reader, indexerHandle);

        var numberType = numberProperty.GetPropertyType();
        var primitiveNumber = Assert.IsType<PrimitiveTypeSignature>(numberType);
        Assert.Equal(PrimitiveTypeCode.Int32, primitiveNumber.TypeCode);

        var indexerType = indexerContext.GetPropertyType();
        var primitiveIndexer = Assert.IsType<PrimitiveTypeSignature>(indexerType);
        Assert.Equal(PrimitiveTypeCode.String, primitiveIndexer.TypeCode);
    }

    [Fact]
    public void IndexerHelpers_DistinguishSimplePropertyAndIndexer()
    {
        using var metadata = TestUtility.BuildMetadata(AccessorSignatureSource);
        var reader = metadata.Reader;
        var numberProperty = GetPropertyDefinition(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "Number");
        var indexerProperty = GetPropertyDefinition(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "Item");
        var indexerContext = PropertyContext.Create(
            reader,
            GetPropertyHandle(metadata, "Glean.Tests.Extensions", "AccessorSignatureFixture", "Item"));

        Assert.False(numberProperty.IsIndexer(reader));
        Assert.Equal(0, numberProperty.GetIndexParameterCount(reader));

        Assert.True(indexerProperty.IsIndexer(reader));
        Assert.Equal(1, indexerProperty.GetIndexParameterCount(reader));
        Assert.True(indexerContext.IsIndexer());
    }

    private static EventDefinitionHandle GetEventHandle(
        MetadataScope metadata,
        string ns,
        string typeName,
        string eventName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        foreach (var handle in typeDefinition.GetEvents())
        {
            var eventDefinition = metadata.Reader.GetEventDefinition(handle);
            if (eventDefinition.NameIs(metadata.Reader, eventName))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not locate event {typeName}.{eventName}.");
    }

    private static EventDefinition GetEventDefinition(
        MetadataScope metadata,
        string ns,
        string typeName,
        string eventName)
    {
        return metadata.Reader.GetEventDefinition(GetEventHandle(metadata, ns, typeName, eventName));
    }

    private static PropertyDefinitionHandle GetPropertyHandle(
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
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not locate property {typeName}.{propertyName}.");
    }

    private static PropertyDefinition GetPropertyDefinition(
        MetadataScope metadata,
        string ns,
        string typeName,
        string propertyName)
    {
        return metadata.Reader.GetPropertyDefinition(GetPropertyHandle(metadata, ns, typeName, propertyName));
    }
}

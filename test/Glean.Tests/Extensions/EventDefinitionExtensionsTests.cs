using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class EventDefinitionExtensionsTests
{
    private const string EventSource = """
        using System;

        namespace Glean.Tests.Extensions;

        internal sealed class EventFixture
        {
            internal event Action? Changed;

            internal void RaiseChanged()
            {
                Changed?.Invoke();
            }
        }
        """;

    // == Accessors ===========================================================

    [Fact]
    public void AccessorHelpers_CompiledEvent_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(EventSource);
        var eventDefinition = GetEventDefinition(metadata, "Glean.Tests.Extensions", "EventFixture", "Changed");

        Assert.True(eventDefinition.HasAdder());
        Assert.True(eventDefinition.HasRemover());
        Assert.False(eventDefinition.HasRaiser());
        Assert.True(eventDefinition.NameIs(metadata.Reader, "Changed"));
        Assert.Equal("add_Changed", GetMethodName(metadata.Reader, eventDefinition.GetAdder()));
        Assert.Equal("remove_Changed", GetMethodName(metadata.Reader, eventDefinition.GetRemover()));
        Assert.True(eventDefinition.GetRaiser().IsNil);
    }

    // == Flags and raiser ====================================================

    [Fact]
    public void FlagAndRaiserHelpers_SyntheticEvent_ReportExpectedValues()
    {
        using var provider = CreateEventMetadataReaderProvider(
            "SyntheticEvent",
            EventAttributes.SpecialName | EventAttributes.RTSpecialName,
            includeRaiser: true);
        var reader = provider.GetMetadataReader();
        var eventDefinition = reader.GetEventDefinition(MetadataTokens.EventDefinitionHandle(1));

        Assert.True(eventDefinition.IsSpecialName());
        Assert.True(eventDefinition.IsRTSpecialName());
        Assert.True(eventDefinition.HasAdder());
        Assert.True(eventDefinition.HasRemover());
        Assert.True(eventDefinition.HasRaiser());
        Assert.Equal("add_SyntheticEvent", GetMethodName(reader, eventDefinition.GetAdder()));
        Assert.Equal("remove_SyntheticEvent", GetMethodName(reader, eventDefinition.GetRemover()));
        Assert.Equal("raise_SyntheticEvent", GetMethodName(reader, eventDefinition.GetRaiser()));
    }

    [Fact]
    public void RaiserHelpers_SyntheticEventWithoutRaiser_ReturnExpectedDefaults()
    {
        using var provider = CreateEventMetadataReaderProvider(
            "PlainEvent",
            0,
            includeRaiser: false);
        var reader = provider.GetMetadataReader();
        var eventDefinition = reader.GetEventDefinition(MetadataTokens.EventDefinitionHandle(1));

        Assert.False(eventDefinition.IsSpecialName());
        Assert.False(eventDefinition.IsRTSpecialName());
        Assert.True(eventDefinition.HasAdder());
        Assert.True(eventDefinition.HasRemover());
        Assert.False(eventDefinition.HasRaiser());
        Assert.True(eventDefinition.GetRaiser().IsNil);
    }

    private static EventDefinition GetEventDefinition(
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
                return eventDefinition;
            }
        }

        throw new InvalidOperationException($"Could not locate event {typeName}.{eventName}.");
    }

    private static string GetMethodName(MetadataReader reader, MethodDefinitionHandle handle)
    {
        return reader.GetString(reader.GetMethodDefinition(handle).Name);
    }

    private static MetadataReaderProvider CreateEventMetadataReaderProvider(
        string eventName,
        EventAttributes attributes,
        bool includeRaiser)
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

        var assemblyReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("System.Runtime"),
            new Version(8, 0, 0, 0),
            default,
            default,
            0,
            default);
        var actionType = metadata.AddTypeReference(
            assemblyReference,
            metadata.GetOrAddString("System"),
            metadata.GetOrAddString("Action"));

        var typeHandle = metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Glean.Tests.Extensions"),
            metadata.GetOrAddString("SyntheticEventFixture"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        var addHandle = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.SpecialName,
            MethodImplAttributes.IL,
            metadata.GetOrAddString($"add_{eventName}"),
            metadata.GetOrAddBlob(CreateVoidMethodSignature()),
            -1,
            MetadataTokens.ParameterHandle(1));
        var removeHandle = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.SpecialName,
            MethodImplAttributes.IL,
            metadata.GetOrAddString($"remove_{eventName}"),
            metadata.GetOrAddBlob(CreateVoidMethodSignature()),
            -1,
            MetadataTokens.ParameterHandle(1));
        MethodDefinitionHandle raiseHandle = default;
        if (includeRaiser)
        {
            raiseHandle = metadata.AddMethodDefinition(
                MethodAttributes.Public | MethodAttributes.SpecialName,
                MethodImplAttributes.IL,
                metadata.GetOrAddString($"raise_{eventName}"),
                metadata.GetOrAddBlob(CreateVoidMethodSignature()),
                -1,
                MetadataTokens.ParameterHandle(1));
        }

        var eventHandle = metadata.AddEvent(
            attributes,
            metadata.GetOrAddString(eventName),
            actionType);
        metadata.AddEventMap(typeHandle, eventHandle);
        metadata.AddMethodSemantics(eventHandle, MethodSemanticsAttributes.Adder, addHandle);
        metadata.AddMethodSemantics(eventHandle, MethodSemanticsAttributes.Remover, removeHandle);
        if (includeRaiser)
        {
            metadata.AddMethodSemantics(eventHandle, MethodSemanticsAttributes.Raiser, raiseHandle);
        }

        var root = new MetadataRootBuilder(metadata);
        var image = new BlobBuilder();
        root.Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }

    private static BlobBuilder CreateVoidMethodSignature()
    {
        var blob = new BlobBuilder();
        new BlobEncoder(blob)
            .MethodSignature()
            .Parameters(
                parameterCount: 0,
                returnType => returnType.Void(),
                parameters => { });
        return blob;
    }
}

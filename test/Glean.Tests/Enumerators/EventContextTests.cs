using System.Reflection.Metadata;

using Xunit;

using Glean.Contexts;
using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Enumerators;

public class EventContextTests
{
    private const string EventContextSource = """
        using System;

        namespace Glean.Tests.Enumerators;

        [AttributeUsage(AttributeTargets.Event)]
        internal sealed class MarkerAttribute : Attribute
        {
        }

        internal sealed class EventContextFixture
        {
            [Marker]
            internal event EventHandler? Changed;

            internal event Action? Plain;
        }
        """;

    // == Metadata Access ======================================================

    [Fact]
    public void MetadataAccess_EventFixture_ReturnsExpectedEventProperties()
    {
        using var metadata = TestUtility.BuildMetadata(EventContextSource);
        var changedEvent = GetEventContext(metadata, "Glean.Tests.Enumerators", "EventContextFixture", "Changed");

        Assert.True(changedEvent.IsValid);
        Assert.Equal("Changed", changedEvent.Name);
        Assert.Equal("EventContextFixture", changedEvent.DeclaringType.Name);
        Assert.Equal(HandleKind.TypeReference, changedEvent.EventType.Kind);

        Assert.True(changedEvent.AddMethod.IsValid);
        Assert.True(changedEvent.RemoveMethod.IsValid);
        Assert.False(changedEvent.RaiseMethod.IsValid);

        Assert.Equal("add_Changed", changedEvent.AddMethod.Name);
        Assert.Equal("remove_Changed", changedEvent.RemoveMethod.Name);
    }

    [Fact]
    public void AttributeAccess_EventFixture_ReturnsExpectedAttributeBehavior()
    {
        using var metadata = TestUtility.BuildMetadata(EventContextSource);
        var changedEvent = GetEventContext(metadata, "Glean.Tests.Enumerators", "EventContextFixture", "Changed");
        var plainEvent = GetEventContext(metadata, "Glean.Tests.Enumerators", "EventContextFixture", "Plain");

        var allAttributes = changedEvent.EnumerateCustomAttributes();
        Assert.True(allAttributes.MoveNext());
        Assert.Equal("MarkerAttribute", GetAttributeTypeName(allAttributes.Current));
        Assert.False(allAttributes.MoveNext());

        var filtered = changedEvent.EnumerateAttributes("Glean.Tests.Enumerators", "MarkerAttribute");
        Assert.True(filtered.MoveNext());
        Assert.Equal("MarkerAttribute", GetAttributeTypeName(filtered.Current));
        Assert.False(filtered.MoveNext());

        Assert.True(changedEvent.HasAttribute("Glean.Tests.Enumerators", "MarkerAttribute"));
        Assert.True(changedEvent.TryFindAttribute("Glean.Tests.Enumerators", "MarkerAttribute", out var attribute));
        Assert.Equal("MarkerAttribute", GetAttributeTypeName(attribute));

        Assert.False(plainEvent.HasAttribute("Glean.Tests.Enumerators", "MarkerAttribute"));
        Assert.False(plainEvent.TryFindAttribute("Glean.Tests.Enumerators", "MarkerAttribute", out var missing));
        Assert.Equal(default, missing);
    }

    private static EventContext GetEventContext(
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
                return EventContext.Create(metadata.Reader, handle);
            }
        }

        throw new InvalidOperationException($"Could not locate event {typeName}.{eventName}.");
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

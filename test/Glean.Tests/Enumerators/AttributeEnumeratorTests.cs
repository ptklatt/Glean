using System.Collections.Generic;
using System.Reflection.Metadata;

using Xunit;

using Glean.Contexts;
using Glean.Tests.Utility;

namespace Glean.Tests.Enumerators;

public class AttributeEnumeratorTests
{
    private const string AttributeEnumerationSource = """
        using System;

        namespace Glean.Tests.Enumerators;

        [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
        internal sealed class MarkerAttribute : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
        internal sealed class NoiseAttribute : Attribute
        {
        }

        [Marker]
        [Noise]
        internal sealed class AttributeEnumerationFixture
        {
            [Noise]
            [Marker]
            private readonly int _field;

            [Marker]
            [Noise]
            public int Number { get; set; }

            [Marker]
            public event EventHandler Changed = delegate { };

            [Marker]
            [Noise]
            public void MarkedMethod()
            {
                _ = _field;
            }
        }
        """;

    // == Custom Attribute Enumeration =========================================

    [Fact]
    public void EnumerateCustomAttributes_TypeFixture_ReturnsAllTypeAttributes()
    {
        using var metadata = TestUtility.BuildMetadata(AttributeEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "AttributeEnumerationFixture");
        var enumerator = type.EnumerateCustomAttributes();
        var names = new List<string>();

        while (enumerator.MoveNext())
        {
            names.Add(GetAttributeTypeName(enumerator.Current));
        }

        Assert.Equal(new[] { "MarkerAttribute", "NoiseAttribute" }, names);
    }

    // == Filtered Custom Attribute Enumeration ================================

    [Fact]
    public void EnumerateAttributes_MethodFixture_ReturnsOnlyMatchingAttributes()
    {
        using var metadata = TestUtility.BuildMetadata(AttributeEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "AttributeEnumerationFixture");
        var method = GetMethodContext(type, "MarkedMethod");
        var enumerator = method.EnumerateAttributes("Glean.Tests.Enumerators", "MarkerAttribute");

        Assert.True(enumerator.MoveNext());
        var attribute = enumerator.Current;
        Assert.Equal(HandleKind.MethodDefinition, attribute.Parent.Kind);
        Assert.Equal("MarkerAttribute", GetAttributeTypeName(attribute));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void EnumerateAttributes_MissingAttribute_ReturnsNoResults()
    {
        using var metadata = TestUtility.BuildMetadata(AttributeEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "AttributeEnumerationFixture");
        var method = GetMethodContext(type, "MarkedMethod");
        var enumerator = method.EnumerateAttributes("Glean.Tests.Enumerators", "MissingAttribute");

        Assert.False(enumerator.MoveNext());
    }

    // == All Member Attribute Enumeration =====================================

    [Fact]
    public void EnumerateAllAttributes_TypeFixture_ReturnsMatchingAttributesAcrossMembersInPhaseOrder()
    {
        using var metadata = TestUtility.BuildMetadata(AttributeEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "AttributeEnumerationFixture");
        var enumerator = type.EnumerateAllAttributes("Glean.Tests.Enumerators", "MarkerAttribute");
        var parents = new List<string>();

        while (enumerator.MoveNext())
        {
            parents.Add(GetParentLabel(metadata.Reader, enumerator.Current.Parent));
        }

        Assert.Equal(
            new[]
            {
                "type:AttributeEnumerationFixture",
                "method:MarkedMethod",
                "field:_field",
                "property:Number",
                "event:Changed",
            },
            parents);
    }

    [Fact]
    public void EnumerateAllAttributes_MissingAttribute_ReturnsNoResults()
    {
        using var metadata = TestUtility.BuildMetadata(AttributeEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "AttributeEnumerationFixture");
        var enumerator = type.EnumerateAllAttributes("Glean.Tests.Enumerators", "MissingAttribute");

        Assert.False(enumerator.MoveNext());
    }

    private static TypeContext GetTypeContext(MetadataScope metadata, string ns, string name)
    {
        var handle = metadata.FindTypeDefinitionHandle(ns, name);
        if (handle.IsNil)
        {
            throw new InvalidOperationException($"Could not locate {ns}.{name}.");
        }

        return TypeContext.Create(metadata.Reader, handle);
    }

    private static MethodContext GetMethodContext(TypeContext type, string methodName)
    {
        var enumerator = type.EnumerateMethods();
        while (enumerator.MoveNext())
        {
            var method = enumerator.Current;
            if (method.NameIs(methodName))
            {
                return method;
            }
        }

        throw new InvalidOperationException($"Could not locate method {methodName} on {type.FullName}.");
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

    private static string GetParentLabel(MetadataReader reader, EntityHandle parent)
    {
        return parent.Kind switch
        {
            HandleKind.TypeDefinition => string.Concat(
                "type:",
                reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)parent).Name)),
            HandleKind.MethodDefinition => string.Concat(
                "method:",
                reader.GetString(reader.GetMethodDefinition((MethodDefinitionHandle)parent).Name)),
            HandleKind.FieldDefinition => string.Concat(
                "field:",
                reader.GetString(reader.GetFieldDefinition((FieldDefinitionHandle)parent).Name)),
            HandleKind.PropertyDefinition => string.Concat(
                "property:",
                reader.GetString(reader.GetPropertyDefinition((PropertyDefinitionHandle)parent).Name)),
            HandleKind.EventDefinition => string.Concat(
                "event:",
                reader.GetString(reader.GetEventDefinition((EventDefinitionHandle)parent).Name)),
            _ => throw new InvalidOperationException($"Unexpected parent kind {parent.Kind}."),
        };
    }
}

using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class CustomAttributeExtensionsTests
{
    private const string LocalAttributeSource = """
        using System;

        namespace Glean.Tests.Extensions;

        [AttributeUsage(AttributeTargets.Class)]
        internal sealed class LocalFixtureAttribute : Attribute
        {
        }

        [LocalFixture]
        internal sealed class LocalAttributedType
        {
        }
        """;

    private const string FrameworkAttributeSource = """
        using System;

        namespace Glean.Tests.Extensions;

        [Obsolete("legacy")]
        internal sealed class FrameworkAttributedType
        {
        }
        """;

    // == IsAttributeType ====================================================

    [Fact]
    public void IsAttributeType_LocalAttribute_ReturnsTrue()
    {
        using var metadata = TestUtility.BuildMetadata(LocalAttributeSource);
        var attribute = GetSingleAttribute(metadata, "Glean.Tests.Extensions", "LocalAttributedType");

        bool result = attribute.IsAttributeType(metadata.Reader, "Glean.Tests.Extensions", "LocalFixtureAttribute");

        Assert.True(result);
    }

    [Fact]
    public void IsAttributeType_NonMatchingAttribute_ReturnsFalse()
    {
        using var metadata = TestUtility.BuildMetadata(LocalAttributeSource);
        var attribute = GetSingleAttribute(metadata, "Glean.Tests.Extensions", "LocalAttributedType");

        bool result = attribute.IsAttributeType(metadata.Reader, "System", "ObsoleteAttribute");

        Assert.False(result);
    }

    // == TryFindAttribute ===================================================

    [Fact]
    public void TryFindAttribute_FrameworkAttribute_ReturnsAttribute()
    {
        using var metadata = TestUtility.BuildMetadata(FrameworkAttributeSource);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "FrameworkAttributedType");

        bool found = typeDef.GetCustomAttributes().TryFindAttribute(
            metadata.Reader,
            "System",
            "ObsoleteAttribute",
            out var attribute);

        Assert.True(found);
        Assert.True(attribute.IsMemberReference());
    }

    [Fact]
    public void TryFindAttribute_MissingAttribute_ReturnsFalse()
    {
        using var metadata = TestUtility.BuildMetadata(FrameworkAttributeSource);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "FrameworkAttributedType");

        bool found = typeDef.GetCustomAttributes().TryFindAttribute(
            metadata.Reader,
            "Glean.Tests.Extensions",
            "LocalFixtureAttribute",
            out var attribute);

        Assert.False(found);
        Assert.Equal(default, attribute);
    }

    // == TryFindAttributeHandle =============================================

    [Fact]
    public void TryFindAttributeHandle_LocalAttribute_ReturnsHandle()
    {
        using var metadata = TestUtility.BuildMetadata(LocalAttributeSource);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "LocalAttributedType");

        bool found = typeDef.GetCustomAttributes().TryFindAttributeHandle(
            metadata.Reader,
            "Glean.Tests.Extensions",
            "LocalFixtureAttribute",
            out var handle);

        Assert.True(found);
        Assert.False(handle.IsNil);
    }

    [Fact]
    public void TryFindAttributeHandle_MissingAttribute_ReturnsFalse()
    {
        using var metadata = TestUtility.BuildMetadata(FrameworkAttributeSource);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "FrameworkAttributedType");

        bool found = typeDef.GetCustomAttributes().TryFindAttributeHandle(
            metadata.Reader,
            "Glean.Tests.Extensions",
            "LocalFixtureAttribute",
            out var handle);

        Assert.False(found);
        Assert.Equal(default, handle);
    }

    // == TryGetAttributeTypeNameHandles =====================================

    [Fact]
    public void TryGetAttributeTypeNameHandles_LocalAttribute_ReturnsNameHandles()
    {
        using var metadata = TestUtility.BuildMetadata(LocalAttributeSource);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "LocalAttributedType");
        var handle = Assert.Single(typeDef.GetCustomAttributes());

        bool found = handle.TryGetAttributeTypeNameHandles(metadata.Reader, out var ns, out var name);

        Assert.True(found);
        Assert.True(metadata.Reader.StringComparer.Equals(ns, "Glean.Tests.Extensions"));
        Assert.True(metadata.Reader.StringComparer.Equals(name, "LocalFixtureAttribute"));
    }

    [Fact]
    public void TryGetAttributeTypeNameHandles_FrameworkAttribute_ReturnsNameHandles()
    {
        using var metadata = TestUtility.BuildMetadata(FrameworkAttributeSource);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "FrameworkAttributedType");
        var handle = Assert.Single(typeDef.GetCustomAttributes());

        bool found = handle.TryGetAttributeTypeNameHandles(metadata.Reader, out var ns, out var name);

        Assert.True(found);
        Assert.True(metadata.Reader.StringComparer.Equals(ns, "System"));
        Assert.True(metadata.Reader.StringComparer.Equals(name, "ObsoleteAttribute"));
    }

    // == Constructor kind helpers ===========================================

    [Fact]
    public void ConstructorKindHelpers_DistinguishLocalAndFrameworkAttributes()
    {
        using var localMetadata = TestUtility.BuildMetadata(LocalAttributeSource);
        using var frameworkMetadata = TestUtility.BuildMetadata(FrameworkAttributeSource);

        var localAttribute = GetSingleAttribute(localMetadata, "Glean.Tests.Extensions", "LocalAttributedType");
        var frameworkAttribute = GetSingleAttribute(frameworkMetadata, "Glean.Tests.Extensions", "FrameworkAttributedType");

        Assert.True(localAttribute.IsMethodDefinition());
        Assert.False(localAttribute.IsMemberReference());
        Assert.False(frameworkAttribute.IsMethodDefinition());
        Assert.True(frameworkAttribute.IsMemberReference());
    }

    private static CustomAttribute GetSingleAttribute(MetadataScope metadata, string ns, string name)
    {
        var typeDef = metadata.GetTypeDefinition(ns, name);
        var handle = Assert.Single(typeDef.GetCustomAttributes());
        return metadata.Reader.GetCustomAttribute(handle);
    }
}

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class TypeReferenceExtensionsTests
{
    // == Identity checks =====================================================

    [Fact]
    public void IdentityChecks_SystemObjectReference_MatchExpectedIdentity()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeReferenceExtensionsTests).Assembly);
        var objectTypeRef = metadata.GetTypeReference("System", "Object");
        var stringTypeRef = metadata.GetTypeReference("System", "String");

        Assert.True(objectTypeRef.Is(metadata.Reader, "System", "Object"));
        Assert.False(objectTypeRef.Is(metadata.Reader, "System", "String"));

        Assert.True(objectTypeRef.Is(objectTypeRef.Namespace, objectTypeRef.Name));
        Assert.False(objectTypeRef.Is(objectTypeRef.Namespace, stringTypeRef.Name));
    }

    [Fact]
    public void NameAndNamespaceChecks_GenericListReference_MatchExpectedParts()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeReferenceExtensionsTests).Assembly);
        var listTypeRef = metadata.GetTypeReference("System.Collections.Generic", "List`1");

        Assert.True(listTypeRef.NameIs(metadata.Reader, "List`1"));
        Assert.False(listTypeRef.NameIs(metadata.Reader, "Dictionary`2"));
        Assert.True(listTypeRef.NameIs(listTypeRef.Name));

        Assert.True(listTypeRef.NamespaceIs(metadata.Reader, "System.Collections.Generic"));
        Assert.False(listTypeRef.NamespaceIs(metadata.Reader, "System"));
        Assert.True(listTypeRef.NamespaceIs(listTypeRef.Namespace));
    }

    // == Formatting ==========================================================

    [Fact]
    public void ToFullNameString_SystemObjectReference_ReturnsCombinedName()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeReferenceExtensionsTests).Assembly);
        var typeRef = metadata.GetTypeReference("System", "Object");

        string fullName = typeRef.ToFullNameString(metadata.Reader);

        Assert.Equal("System.Object", fullName);
    }
}

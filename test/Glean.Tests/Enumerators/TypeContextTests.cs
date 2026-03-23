using System.Reflection.Metadata;

using Xunit;

using Glean.Contexts;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Enumerators;

public sealed class TypeContextTests
{
    [Fact]
    public void BaseTypeAccess_TypeReferenceBase_UsesTypedReferenceHelper()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeContextTests).Assembly);
        var type = GetTypeContext(metadata, "Glean.Tests.Extensions", "PublicVisibleFixture");

        Assert.True(type.HasBaseType);
        Assert.False(type.TryGetBaseTypeDefinition(out var localBase));
        Assert.Equal(default, localBase);
        Assert.True(type.TryGetBaseTypeReference(out var referencedBase));
        Assert.False(type.TryGetBaseTypeSpecification(out var specificationHandle));
        Assert.True(specificationHandle.IsNil);

        Assert.True(referencedBase.Is("System", "Object"));
        Assert.Equal("System.Object", type.GetBaseTypeName());

        var baseType = Assert.IsType<TypeReferenceSignature>(type.DecodeBaseType());
        Assert.True(baseType.Is("System", "Object"));
    }

    [Fact]
    public void BaseTypeAccess_TypeDefinitionBase_UsesTypedDefinitionHelper()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeContextTests).Assembly);
        var type = GetTypeContext(metadata, "Glean.Tests.Extensions", "DerivedFromLocalBaseFixture");

        Assert.True(type.HasBaseType);
        Assert.True(type.TryGetBaseTypeDefinition(out var localBase));
        Assert.False(type.TryGetBaseTypeReference(out var referencedBase));
        Assert.Equal(default, referencedBase);
        Assert.False(type.TryGetBaseTypeSpecification(out var specificationHandle));
        Assert.True(specificationHandle.IsNil);

        Assert.True(localBase.Is("Glean.Tests.Extensions", "LocalBaseFixture"));
        Assert.Equal("Glean.Tests.Extensions.LocalBaseFixture", type.GetBaseTypeName());

        var baseType = Assert.IsType<TypeDefinitionSignature>(type.DecodeBaseType());
        Assert.True(baseType.Is("Glean.Tests.Extensions", "LocalBaseFixture"));
    }

    [Fact]
    public void BaseTypeAccess_TypeSpecificationBase_UsesSpecificationHelperAndDecode()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeContextTests).Assembly);
        var type = GetTypeContext(metadata, "Glean.Tests.Extensions", "DerivedFromGenericBaseFixture");

        Assert.True(type.HasBaseType);
        Assert.False(type.TryGetBaseTypeDefinition(out var localBase));
        Assert.Equal(default, localBase);
        Assert.False(type.TryGetBaseTypeReference(out var referencedBase));
        Assert.Equal(default, referencedBase);
        Assert.True(type.TryGetBaseTypeSpecification(out var specificationHandle));
        Assert.False(specificationHandle.IsNil);

        Assert.Null(type.GetBaseTypeName());

        var baseType = Assert.IsType<GenericInstanceSignature>(type.DecodeBaseType());
        Assert.True(baseType.GenericType.Is("System.Collections.Generic", "List`1"));
        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(baseType.Arguments));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);
    }

    [Fact]
    public void BaseTypeAccess_SystemObject_HasNoBaseType()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var type = GetTypeContext(metadata, "System", "Object");

        Assert.False(type.HasBaseType);
        Assert.False(type.TryGetBaseTypeDefinition(out var localBase));
        Assert.Equal(default, localBase);
        Assert.False(type.TryGetBaseTypeReference(out var referencedBase));
        Assert.Equal(default, referencedBase);
        Assert.False(type.TryGetBaseTypeSpecification(out var specificationHandle));
        Assert.True(specificationHandle.IsNil);
        Assert.Null(type.GetBaseTypeName());
        Assert.Null(type.DecodeBaseType());
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
}

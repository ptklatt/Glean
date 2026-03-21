using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class InterfaceImplementationExtensionsTests
{
    // == Decode ==============================================================

    [Fact]
    public void DecodeInterfaceTypeSignature_GenericInterfaceImplementation_ReturnsGenericInstanceSignature()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(InterfaceImplementationExtensionsTests).Assembly);
        var interfaceImplementation = GetSingleInterfaceImplementation(
            metadata,
            "Glean.Tests.Extensions",
            "GenericDirectInterfaceImplFixture");

        TypeSignature signature = interfaceImplementation.DecodeInterfaceTypeSignature(metadata.Reader);

        var genericInstance = Assert.IsType<GenericInstanceSignature>(signature);
        Assert.True(genericInstance.GenericType.Is("Glean.Tests.Extensions", "IGenericDirectInterfaceFixture`1"));

        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(genericInstance.Arguments));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);
    }

    private static InterfaceImplementation GetSingleInterfaceImplementation(MetadataScope metadata, string ns, string name)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, name);
        var handle = Assert.Single(typeDefinition.GetInterfaceImplementations());
        return metadata.Reader.GetInterfaceImplementation(handle);
    }
}

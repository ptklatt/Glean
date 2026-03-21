using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class GenericParameterConstraintExtensionsTests
{
    private const string GenericConstraintSource = """
        using System.Collections.Generic;

        namespace Glean.Tests.Extensions;

        internal sealed class GenericConstraintFixture<T>
            where T : IEnumerable<int>
        {
        }
        """;

    // == Decode ==============================================================

    [Fact]
    public void DecodeConstraintTypeSignature_GenericConstraint_ReturnsGenericInstanceSignature()
    {
        using var metadata = TestUtility.BuildMetadata(GenericConstraintSource);
        var genericParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericConstraintFixture`1",
            "T");
        var constraintHandle = Assert.Single(genericParameter.GetConstraintHandles());
        var constraint = metadata.Reader.GetGenericParameterConstraint(constraintHandle);

        TypeSignature signature = constraint.DecodeConstraintTypeSignature(metadata.Reader);

        var genericInstance = Assert.IsType<GenericInstanceSignature>(signature);
        Assert.True(genericInstance.GenericType.Is("System.Collections.Generic", "IEnumerable`1"));

        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(genericInstance.Arguments));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);
    }

    private static GenericParameter GetTypeGenericParameter(
        MetadataScope metadata,
        string ns,
        string typeName,
        string parameterName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        foreach (var handle in typeDefinition.GetGenericParameters())
        {
            var genericParameter = metadata.Reader.GetGenericParameter(handle);
            if (genericParameter.NameIs(metadata.Reader, parameterName))
            {
                return genericParameter;
            }
        }

        throw new InvalidOperationException($"Could not locate generic parameter {parameterName}.");
    }
}

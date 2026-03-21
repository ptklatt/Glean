using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;

namespace Glean.Tests.Utility;

public class TestAssemblyBuilderTests
{
    [Fact]
    public void BuildMetadataScope_CompilesSourceAndSupportsTypeLookup()
    {
        using var metadata = new TestAssemblyBuilder()
            .WithSource("""
                namespace Example;

                public class TestClass : System.Object
                {
                }
                """)
            .BuildMetadataScope();

        var typeDef = metadata.GetTypeDefinition("Example", "TestClass");
        var baseType = metadata.GetBaseTypeReference("Example", "TestClass");

        Assert.Equal("TestClass", metadata.Reader.GetString(typeDef.Name));
        Assert.True(baseType.Is(metadata.Reader, "System", "Object"));
    }

    [Fact]
    public void BuildMetadataScope_GenericBaseTypeSupportsTypeSpecificationLookup()
    {
        using var metadata = TestUtility.BuildMetadata(
            """
            using System.Collections.Generic;

            namespace Example;

            public class TestClass : List<int>
            {
            }
            """);

        TypeSpecificationHandle handle = metadata.GetBaseTypeSpecificationHandle("Example", "TestClass");

        Assert.False(handle.IsNil);
    }
}

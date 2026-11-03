using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;

namespace Glean.Tests.Utility;

public class TestAssemblyBuilderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildPEStream_MultiModuleReference_DoesNotRequireFixtureFiles(bool aliased)
    {
        using var workspace = new TemporaryDirectory();
        var modulePath = TestAssemblyFiles.BuildModuleFile(workspace.Path, "Shared",
            "namespace Fixture; public class SharedType { }");
        var assemblyPath = Path.Combine(workspace.Path, "Dependency.dll");
        using (var image = new TestAssemblyBuilder("Dependency")
            .WithModuleReference(modulePath)
            .WithSource("namespace Fixture; public class Dependency { public SharedType Value => new SharedType(); }")
            .BuildPEStream())
        {
            File.WriteAllBytes(assemblyPath, image.ToArray());
        }

        var builder = new TestAssemblyBuilder("Consumer");
        if (aliased)
        {
            builder.WithReference(assemblyPath, "dependency")
                .WithSource("extern alias dependency; public class Consumer : dependency::Fixture.SharedType { }");
        }
        else
        {
            builder.WithReference(assemblyPath)
                .WithSource("public class Consumer : Fixture.SharedType { }");
        }

        File.Delete(modulePath);
        File.Delete(assemblyPath);

        using var first = builder.BuildMetadataScope();
        using var second = builder.BuildMetadataScope();
        Assert.True(first.GetBaseTypeReference("", "Consumer").Is(first.Reader, "Fixture", "SharedType"));
        Assert.True(second.GetBaseTypeReference("", "Consumer").Is(second.Reader, "Fixture", "SharedType"));
    }

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

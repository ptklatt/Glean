using Xunit;

using Glean.Tests.Utility;

namespace Glean.Tests;

public sealed class MetadataFactoryTests
{
    [Fact]
    public unsafe void CreateFromPointer_MetadataBlob_ReturnsReader()
    {
        using var buffer = new TestAssemblyBuilder("PointerAssembly")
            .WithSource(
                """
                namespace Fixture;

                public sealed class Value
                {
                }
                """)
            .BuildUnsafePointer();

        var reader = MetadataFactory.CreateFromPointer(buffer.Pointer, buffer.Length);

        Assert.True(reader.IsAssembly);
        Assert.Equal("PointerAssembly", reader.GetString(reader.GetAssemblyDefinition().Name));
    }

    [Fact]
    public unsafe void CreateOwnedFromArray_MetadataBlob_ReturnsReader()
    {
        using var buffer = new TestAssemblyBuilder("ArrayAssembly")
            .WithSource(
                """
                namespace Fixture;

                public sealed class Value
                {
                }
                """)
            .BuildUnsafePointer();

        var data = new ReadOnlySpan<byte>(buffer.Pointer, buffer.Length).ToArray();

        using var scope = MetadataFactory.CreateOwnedFromArray(data);

        Assert.True(scope.Reader.IsAssembly);
        Assert.Equal("ArrayAssembly", scope.Reader.GetString(scope.Reader.GetAssemblyDefinition().Name));
    }

    [Fact]
    public unsafe void CreateOwnedFromSpan_MetadataBlob_ReturnsReader()
    {
        using var buffer = new TestAssemblyBuilder("SpanAssembly")
            .WithSource(
                """
                namespace Fixture;

                public sealed class Value
                {
                }
                """)
            .BuildUnsafePointer();

        var data = new ReadOnlySpan<byte>(buffer.Pointer, buffer.Length);

        using var scope = MetadataFactory.CreateOwnedFromSpan(data);

        Assert.True(scope.Reader.IsAssembly);
        Assert.Equal("SpanAssembly", scope.Reader.GetString(scope.Reader.GetAssemblyDefinition().Name));
    }
}

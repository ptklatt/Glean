using Xunit;

using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyScopeTests
{
    [Fact]
    public void Open_ReturnsReaderContextAndPeReader()
    {
        using var workspace = new TemporaryDirectory();

        var assemblyPath = TestAssemblyFiles.BuildAssemblyFile(
            workspace.Path,
            "ScopedAssembly",
            """
            namespace Fixture;

            public sealed class Value
            {
                public int Number => 42;
            }
            """);

        using var scope = AssemblyScope.Open(assemblyPath);

        Assert.True(scope.Reader.IsAssembly);
        Assert.True(scope.PeReader.HasMetadata);
        Assert.Equal("ScopedAssembly", scope.Context.Name);
    }
}

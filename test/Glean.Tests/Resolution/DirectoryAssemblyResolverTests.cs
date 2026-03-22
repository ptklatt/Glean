using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class DirectoryAssemblyResolverTests
{
    [Fact]
    public void TryResolve_AssemblyReference_LoadsReaderAndCachesResult()
    {
        using var workspace = new TemporaryDirectory();

        var referenceDir = workspace.CreateDirectory("refs");
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
            referenceDir,
            "DependencyLibrary",
            """
            namespace Fixture;

            public sealed class Dependency
            {
            }
            """);

        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    private readonly Dependency _dependency = new();
                }
                """)
            .WithReference(dependencyPath)
            .BuildMetadataScope();

        var assemblyReferenceHandle = ResolutionTestHelpers.FindAssemblyReferenceHandle(consumer.Reader, "DependencyLibrary");

        using var resolver = new DirectoryAssemblyResolver(referenceDir);
        Assert.True(resolver.TryResolve(assemblyReferenceHandle, consumer.Reader, out var firstReader));
        Assert.NotNull(firstReader);

        Assert.True(resolver.TryResolve(assemblyReferenceHandle, consumer.Reader, out var secondReader));
        Assert.Same(firstReader, secondReader);
        Assert.Equal("DependencyLibrary", firstReader!.GetString(firstReader.GetAssemblyDefinition().Name));
    }

    [Fact]
    public void TryResolve_ProbesNuGetLayoutWhenConfigured()
    {
        using var workspace = new TemporaryDirectory();

        var packageRoot = workspace.CreateDirectory("package");
        var libDirectory = Path.Combine(packageRoot, "lib", "net9.0");
        Directory.CreateDirectory(libDirectory);

        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
            libDirectory,
            "DependencyLibrary",
            """
            namespace Fixture;

            public sealed class Dependency
            {
            }
            """);

        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    private readonly Dependency _dependency = new();
                }
                """)
            .WithReference(dependencyPath)
            .BuildMetadataScope();

        var assemblyReferenceHandle = ResolutionTestHelpers.FindAssemblyReferenceHandle(consumer.Reader, "DependencyLibrary");

        using (var plainResolver = new DirectoryAssemblyResolver(packageRoot))
        {
            Assert.False(plainResolver.TryResolve(assemblyReferenceHandle, consumer.Reader, out var unresolvedReader));
            Assert.Null(unresolvedReader);
        }

        using var nugetResolver = new DirectoryAssemblyResolver("net9.0", new[] { packageRoot });
        Assert.True(nugetResolver.TryResolve(assemblyReferenceHandle, consumer.Reader, out var resolvedReader));
        Assert.NotNull(resolvedReader);
        Assert.Equal("DependencyLibrary", resolvedReader!.GetString(resolvedReader.GetAssemblyDefinition().Name));
    }

    [Fact]
    public void TryResolve_ModuleReference_PrefersRequestingAssemblyDirectory()
    {
        using var workspace = new TemporaryDirectory();

        var hostADir = workspace.CreateDirectory("hostA");
        var hostBDir = workspace.CreateDirectory("hostB");

        var moduleAPath = TestAssemblyFiles.BuildModuleFile(
            hostADir,
            "Shared",
            """
            namespace ModuleA;

            public sealed class SharedType
            {
            }
            """);

        var moduleBPath = TestAssemblyFiles.BuildModuleFile(
            hostBDir,
            "Shared",
            """
            namespace ModuleB;

            public sealed class SharedType
            {
            }
            """);

        var hostAPath = Path.Combine(hostADir, "HostA.dll");
        using (var hostAStream = new TestAssemblyBuilder("HostA")
            .WithModuleReference(moduleAPath)
            .WithSource(
                """
                using ModuleA;

                namespace Fixture;

                public sealed class HostA
                {
                    public SharedType? Value => null;
                }
                """)
            .BuildPEStream())
        {
            File.WriteAllBytes(hostAPath, hostAStream.ToArray());
        }

        var hostBPath = Path.Combine(hostBDir, "HostB.dll");
        using (var hostBStream = new TestAssemblyBuilder("HostB")
            .WithModuleReference(moduleBPath)
            .WithSource(
                """
                using ModuleB;

                namespace Fixture;

                public sealed class HostB
                {
                    public SharedType? Value => null;
                }
                """)
            .BuildPEStream())
        {
            File.WriteAllBytes(hostBPath, hostBStream.ToArray());
        }

        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithReference(hostAPath)
            .WithReference(hostBPath)
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    public void Run(HostA a, HostB b)
                    {
                        _ = a.Value;
                        _ = b.Value;
                    }
                }
                """)
            .BuildMetadataScope();

        using var resolver = new DirectoryAssemblyResolver(hostADir, hostBDir);

        var hostAReference = ResolutionTestHelpers.FindAssemblyReferenceHandle(consumer.Reader, "HostA");
        Assert.True(resolver.TryResolve(hostAReference, consumer.Reader, out var hostAReader));
        Assert.NotNull(hostAReader);

        var hostBReference = ResolutionTestHelpers.FindAssemblyReferenceHandle(consumer.Reader, "HostB");
        Assert.True(resolver.TryResolve(hostBReference, consumer.Reader, out var hostBReader));
        Assert.NotNull(hostBReader);

        Assert.Equal(1, hostAReader!.GetTableRowCount(TableIndex.ModuleRef));
        Assert.Equal(1, hostBReader!.GetTableRowCount(TableIndex.ModuleRef));

        var hostAModuleReference = MetadataTokens.ModuleReferenceHandle(1);
        Assert.True(resolver.TryResolve(hostAModuleReference, hostAReader, out var moduleAReader));
        Assert.NotNull(moduleAReader);
        Assert.False(moduleAReader!.IsAssembly);
        Assert.True(ContainsType(moduleAReader, "ModuleA", "SharedType"));

        var hostBModuleReference = MetadataTokens.ModuleReferenceHandle(1);
        Assert.True(resolver.TryResolve(hostBModuleReference, hostBReader, out var moduleBReader));
        Assert.NotNull(moduleBReader);
        Assert.False(moduleBReader!.IsAssembly);
        Assert.True(ContainsType(moduleBReader, "ModuleB", "SharedType"));
        Assert.NotSame(moduleAReader, moduleBReader);
    }

    private static bool ContainsType(MetadataReader reader, string nameSpace, string name)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDefinition = reader.GetTypeDefinition(handle);
            if ((reader.GetString(typeDefinition.Namespace) == nameSpace) &&
                (reader.GetString(typeDefinition.Name) == name))
            {
                return true;
            }
        }

        return false;
    }
}

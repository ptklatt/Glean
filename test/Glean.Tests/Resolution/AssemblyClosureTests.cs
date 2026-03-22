using System.Reflection.Metadata;
using System.Reflection;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblyClosureTests
{
    [Fact]
    public void Load_ResolvesCrossAssemblyReferences()
    {
        using var workspace = new TemporaryDirectory();

        var referenceDir = workspace.CreateDirectory("refs");
        var appDir = workspace.CreateDirectory("app");

        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(
            referenceDir,
            "DependencyLibrary",
            """
            namespace Fixture;

            public sealed class Dependency
            {
                public int Echo(int value)
                {
                    return value;
                }
            }
            """);

        var entryPath = TestAssemblyFiles.BuildAssemblyFile(
            appDir,
            "EntryAssembly",
            """
            using Fixture;

            namespace Fixture;

            public sealed class EntryPoint
            {
                public int Run(Dependency dependency)
                {
                    return dependency.Echo(42);
                }
            }
            """,
            dependencyPath);

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        Assert.NotNull(runtimeDir);

        using var closure = AssemblyClosure.Load(entryPath, appDir, referenceDir, runtimeDir!);
        Assert.Same(closure, closure.ThrowIfPartial());

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(closure.EntryReader, "Fixture", "Dependency");
        Assert.True(closure.Set.TryResolveType(closure.EntryReader, typeReferenceHandle, out var typeReader, out var typeHandle));
        Assert.Equal("Dependency", typeReader.GetString(typeReader.GetTypeDefinition(typeHandle).Name));

        var memberReferenceHandle = ResolutionTestHelpers.FindMemberReferenceHandle(closure.EntryReader, "Fixture", "Dependency", "Echo");
        Assert.True(closure.Set.TryResolveMember(closure.EntryReader, memberReferenceHandle, out var memberReader, out var memberHandle));
        Assert.Same(typeReader, memberReader);
        Assert.Equal(HandleKind.MethodDefinition, memberHandle.Kind);

        Assert.Contains(Path.GetFullPath(entryPath), closure.LoadedAssemblies.Keys);
        Assert.Contains(Path.GetFullPath(dependencyPath), closure.LoadedAssemblies.Keys);
    }

    [Fact]
    public void ThrowIfPartial_ThrowsPartialClosureException()
    {
        using var workspace = new TemporaryDirectory();

        var referenceDir = workspace.CreateDirectory("refs");
        var appDir = workspace.CreateDirectory("app");
        _ = TestAssemblyFiles.BuildAssemblyFile(
            referenceDir,
            "MissingDependency",
            """
            namespace Fixture;

            public static class MissingDependency
            {
                public static void Touch()
                {
                }
            }
            """);

        var entryPath = TestAssemblyFiles.BuildAssemblyFile(
            appDir,
            "EntryAssembly",
            """
            using Fixture;

            namespace Fixture;

            public sealed class EntryPoint
            {
                public void Run()
                {
                    MissingDependency.Touch();
                }
            }
            """,
            Path.Combine(referenceDir, "MissingDependency.dll"));

        var brokenDependencyPath = Path.Combine(appDir, "MissingDependency.dll");
        File.WriteAllText(brokenDependencyPath, "not a PE image");

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        Assert.NotNull(runtimeDir);

        using var closure = AssemblyClosure.Load(entryPath, appDir, runtimeDir!);

        var exception = Assert.Throws<PartialClosureException>(() => closure.ThrowIfPartial());
        var failure = Assert.Single(exception.SkippedDependencies, static f => f.AssemblySimpleName == "MissingDependency");
        Assert.Equal(AssemblyDependencyLoadFailureKind.BadImageFormat, failure.Kind);
        Assert.Equal(Path.GetFullPath(brokenDependencyPath), failure.Path);
    }
}

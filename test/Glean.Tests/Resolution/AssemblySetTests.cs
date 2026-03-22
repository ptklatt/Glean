using System.Reflection.Metadata;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblySetTests
{
    [Fact]
    public void TryResolveType_ReferencedAssembly_ResolvesTypeDefinition()
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

        using var dependency = new MetadataScope(dependencyPath);
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

        using var set = new AssemblySet();
        set.Add(dependency.Reader);

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "Dependency");

        Assert.True(set.TryResolveType(consumer.Reader, typeReferenceHandle, out var targetReader, out var targetHandle));
        Assert.Same(dependency.Reader, targetReader);
        Assert.Equal("Dependency", targetReader.GetString(targetReader.GetTypeDefinition(targetHandle).Name));
    }

    [Fact]
    public void TryResolveMember_ReferencedAssembly_ResolvesMethodAndField()
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
                public int Value;

                public int Echo(int value)
                {
                    Value = value;
                    return value;
                }
            }
            """);

        using var dependency = new MetadataScope(dependencyPath);
        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    public int Run(Dependency dependency)
                    {
                        dependency.Value = dependency.Echo(42);
                        return dependency.Value;
                    }
                }
                """)
            .WithReference(dependencyPath)
            .BuildMetadataScope();

        using var set = new AssemblySet();
        set.Add(dependency.Reader);

        var methodReferenceHandle = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "Dependency", "Echo");
        Assert.True(set.TryResolveMember(consumer.Reader, methodReferenceHandle, out var methodReader, out var methodHandle));
        Assert.Same(dependency.Reader, methodReader);
        Assert.Equal(HandleKind.MethodDefinition, methodHandle.Kind);
        Assert.Equal("Echo", methodReader.GetString(methodReader.GetMethodDefinition((MethodDefinitionHandle)methodHandle).Name));

        var fieldReferenceHandle = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "Dependency", "Value");
        var index = new MemberResolutionIndex();
        Assert.True(set.TryResolveMember(consumer.Reader, fieldReferenceHandle, index, out var fieldReader, out var fieldHandle));
        Assert.Same(dependency.Reader, fieldReader);
        Assert.Equal(HandleKind.FieldDefinition, fieldHandle.Kind);
        Assert.Equal("Value", fieldReader.GetString(fieldReader.GetFieldDefinition((FieldDefinitionHandle)fieldHandle).Name));
    }

    [Fact]
    public void TryResolveMember_DistinguishesSameFullNameTypesFromDifferentAssemblies()
    {
        using var workspace = new TemporaryDirectory();

        var dependencyADir = workspace.CreateDirectory("depA");
        var dependencyBDir = workspace.CreateDirectory("depB");
        var targetDir = workspace.CreateDirectory("target");

        var dependencyAPath = TestAssemblyFiles.BuildAssemblyFile(
            dependencyADir,
            "CollisionA",
            """
            namespace Fixture;

            public sealed class Collision
            {
            }
            """);

        var dependencyBPath = TestAssemblyFiles.BuildAssemblyFile(
            dependencyBDir,
            "CollisionB",
            """
            namespace Fixture;

            public sealed class Collision
            {
            }
            """);

        var targetPath = Path.Combine(targetDir, "TargetLibrary.dll");
        using (var targetStream = new TestAssemblyBuilder("TargetLibrary")
            .WithReference(dependencyAPath, "A")
            .WithReference(dependencyBPath, "B")
            .WithSource(
                """
                extern alias A;
                extern alias B;

                namespace Fixture;

                public sealed class Service
                {
                    public string Choose(A::Fixture.Collision value)
                    {
                        return "A";
                    }

                    public string Choose(B::Fixture.Collision value)
                    {
                        return "B";
                    }
                }
                """)
            .BuildPEStream())
        {
            File.WriteAllBytes(targetPath, targetStream.ToArray());
        }

        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithReference(targetPath)
            .WithReference(dependencyAPath, "A")
            .WithReference(dependencyBPath, "B")
            .WithSource(
                """
                extern alias A;
                extern alias B;

                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    public string Run(Service service, A::Fixture.Collision value)
                    {
                        return service.Choose(value);
                    }
                }
                """)
            .BuildMetadataScope();

        using var target = new MetadataScope(targetPath);
        using var set = new AssemblySet();
        set.Add(target.Reader);

        var memberReferenceHandle = ResolutionTestHelpers.FindMemberReferenceHandle(
            consumer.Reader,
            "Fixture",
            "Service",
            "Choose");

        Assert.True(set.TryResolveMember(consumer.Reader, memberReferenceHandle, out var methodReader, out var methodHandle));

        var method = methodReader.GetMethodDefinition((MethodDefinitionHandle)methodHandle);
        var signature = method.DecodeSignature(Glean.Providers.SignatureTypeProvider.Instance, Glean.Providers.SignatureDecodeContext.Empty);
        var parameterType = Assert.IsType<Glean.Signatures.TypeReferenceSignature>(Assert.Single(signature.ParameterTypes));
        Assert.Equal("CollisionA", parameterType.ResolutionScopeName);
        Assert.True(parameterType.Is("Fixture", "Collision", "CollisionA"));
    }

    [Fact]
    public void TryResolveType_WithDirectoryAssemblyResolver_ResolvesReferencedAssemblyLazily()
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
                    public Dependency Create()
                    {
                        return new Dependency();
                    }
                }
                """)
            .WithReference(dependencyPath)
            .BuildMetadataScope();

        using var resolver = new DirectoryAssemblyResolver(referenceDir);
        using var set = new AssemblySet(resolver);

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "Dependency");

        Assert.True(set.TryResolveType(consumer.Reader, typeReferenceHandle, out var targetReader, out var targetHandle));
        Assert.Single(set.RegisteredAssemblies);
        Assert.Equal("Dependency", targetReader.GetString(targetReader.GetTypeDefinition(targetHandle).Name));
    }

    [Fact]
    public void TryResolveType_ForwardedAssembly_ResolvesImplementationType()
    {
        using var workspace = new TemporaryDirectory();

        var originalDir = workspace.CreateDirectory("original");
        var implementationDir = workspace.CreateDirectory("implementation");
        var forwarderDir = workspace.CreateDirectory("forwarder");

        var originalPath = TestAssemblyFiles.BuildAssemblyFile(
            originalDir,
            "ForwardedContracts",
            """
            using System.Reflection;

            [assembly: AssemblyVersion("1.0.0.0")]

            namespace Fixture;

            public sealed class ForwardedType
            {
                public int Echo(int value)
                {
                    return value;
                }
            }
            """);

        using var consumer = new TestAssemblyBuilder("ConsumerAssembly")
            .WithSource(
                """
                using Fixture;

                namespace Fixture;

                public sealed class Consumer
                {
                    public int Run(ForwardedType value)
                    {
                        return value.Echo(42);
                    }
                }
                """)
            .WithReference(originalPath)
            .BuildMetadataScope();

        var implementationPath = TestAssemblyFiles.BuildAssemblyFile(
            implementationDir,
            "ForwardedImplementation",
            """
            namespace Fixture;

            public sealed class ForwardedType
            {
                public int Echo(int value)
                {
                    return value;
                }
            }
            """);

        var forwarderPath = TestAssemblyFiles.BuildAssemblyFile(
            forwarderDir,
            "ForwardedContracts",
            """
            using System.Reflection;
            using System.Runtime.CompilerServices;
            using Fixture;

            [assembly: AssemblyVersion("1.0.0.0")]
            [assembly: TypeForwardedTo(typeof(ForwardedType))]
            """,
            implementationPath);

        using var implementation = new MetadataScope(implementationPath);
        using var forwarder = new MetadataScope(forwarderPath);
        using var set = new AssemblySet();
        set.Add(forwarder.Reader);
        set.Add(implementation.Reader);

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "ForwardedType");
        Assert.True(set.TryResolveType(consumer.Reader, typeReferenceHandle, out var typeReader, out var typeHandle));
        Assert.Same(implementation.Reader, typeReader);
        Assert.Equal("ForwardedType", typeReader.GetString(typeReader.GetTypeDefinition(typeHandle).Name));

        var memberReferenceHandle = ResolutionTestHelpers.FindMemberReferenceHandle(consumer.Reader, "Fixture", "ForwardedType", "Echo");
        Assert.True(set.TryResolveMember(consumer.Reader, memberReferenceHandle, out var memberReader, out var memberHandle));
        Assert.Same(implementation.Reader, memberReader);
        Assert.Equal("Echo", memberReader.GetString(memberReader.GetMethodDefinition((MethodDefinitionHandle)memberHandle).Name));
    }

    [Fact]
    public void TryResolveType_ForwardedAssemblyWithoutImplementation_ReturnsAssemblyNotFound()
    {
        using var workspace = new TemporaryDirectory();

        var originalDir = workspace.CreateDirectory("original");
        var implementationDir = workspace.CreateDirectory("implementation");
        var forwarderDir = workspace.CreateDirectory("forwarder");

        var originalPath = TestAssemblyFiles.BuildAssemblyFile(
            originalDir,
            "ForwardedContracts",
            """
            using System.Reflection;

            [assembly: AssemblyVersion("1.0.0.0")]

            namespace Fixture;

            public sealed class ForwardedType
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
                    private readonly ForwardedType _value = new();
                }
                """)
            .WithReference(originalPath)
            .BuildMetadataScope();

        var implementationPath = TestAssemblyFiles.BuildAssemblyFile(
            implementationDir,
            "ForwardedImplementation",
            """
            namespace Fixture;

            public sealed class ForwardedType
            {
            }
            """);

        var forwarderPath = TestAssemblyFiles.BuildAssemblyFile(
            forwarderDir,
            "ForwardedContracts",
            """
            using System.Reflection;
            using System.Runtime.CompilerServices;
            using Fixture;

            [assembly: AssemblyVersion("1.0.0.0")]
            [assembly: TypeForwardedTo(typeof(ForwardedType))]
            """,
            implementationPath);

        using var forwarder = new MetadataScope(forwarderPath);
        using var set = new AssemblySet();
        set.Add(forwarder.Reader);

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "ForwardedType");

        Assert.False(set.TryResolveType(consumer.Reader, typeReferenceHandle, out _, out _, out var reason));
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, reason);
    }

    [Fact]
    public void TryResolveType_WithOnlyHigherVersionRegistered_FallsBackToLooseIdentityMatch()
    {
        using var workspace = new TemporaryDirectory();

        var v1Dir = workspace.CreateDirectory("v1");
        var v2Dir = workspace.CreateDirectory("v2");

        var v1Path = TestAssemblyFiles.BuildAssemblyFile(
            v1Dir,
            "VersionedDependency",
            """
            using System.Reflection;

            [assembly: AssemblyVersion("1.0.0.0")]

            namespace Fixture;

            public sealed class VersionedType
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
                    private readonly VersionedType _value = new();
                }
                """)
            .WithReference(v1Path)
            .BuildMetadataScope();

        var v2Path = TestAssemblyFiles.BuildAssemblyFile(
            v2Dir,
            "VersionedDependency",
            """
            using System.Reflection;

            [assembly: AssemblyVersion("2.0.0.0")]

            namespace Fixture;

            public sealed class VersionedType
            {
            }
            """);

        using var v2Assembly = new MetadataScope(v2Path);
        using var set = new AssemblySet();
        set.Add(v2Assembly.Reader);

        var typeReferenceHandle = ResolutionTestHelpers.FindTypeReferenceHandle(consumer.Reader, "Fixture", "VersionedType");

        Assert.True(set.TryResolveType(consumer.Reader, typeReferenceHandle, out var targetReader, out var targetHandle));
        Assert.Same(v2Assembly.Reader, targetReader);
        Assert.Equal(new Version(2, 0, 0, 0), targetReader.GetAssemblyDefinition().Version);
        Assert.Equal("VersionedType", targetReader.GetString(targetReader.GetTypeDefinition(targetHandle).Name));
    }

    [Fact]
    public void TryResolveMember_TypeSpecificationParent_ResolvesMethodAndField()
    {
        using var metadata = new TestAssemblyBuilder("TypeSpecificationParentFixture")
            .WithSource(
                """
                namespace Fixture;

                public class Base<T>
                {
                    public T Value;

                    public void Use(T value)
                    {
                        Value = value;
                    }
                }

                public sealed class Derived : Base<int>
                {
                    public int ReadAndWrite()
                    {
                        Use(42);
                        return Value;
                    }
                }
                """)
            .BuildMetadataScope();

        using var set = new AssemblySet();
        set.Add(metadata.Reader);

        var methodReferenceHandle = FindTypeSpecificationMemberReference(metadata.Reader, "Use");
        var fieldReferenceHandle = FindTypeSpecificationMemberReference(metadata.Reader, "Value");

        Assert.True(set.TryResolveMember(metadata.Reader, methodReferenceHandle, out var methodReader, out var methodHandle));
        Assert.Same(metadata.Reader, methodReader);
        Assert.Equal(HandleKind.MethodDefinition, methodHandle.Kind);

        var resolvedMethod = methodReader.GetMethodDefinition((MethodDefinitionHandle)methodHandle);
        var declaringType = methodReader.GetTypeDefinition(resolvedMethod.GetDeclaringType());
        Assert.Equal("Base`1", methodReader.GetString(declaringType.Name));

        var index = new MemberResolutionIndex();
        Assert.True(set.TryResolveMember(metadata.Reader, fieldReferenceHandle, index, out var fieldReader, out var fieldHandle));
        Assert.Same(metadata.Reader, fieldReader);
        Assert.Equal(HandleKind.FieldDefinition, fieldHandle.Kind);

        var resolvedField = fieldReader.GetFieldDefinition((FieldDefinitionHandle)fieldHandle);
        Assert.Equal("Value", fieldReader.GetString(resolvedField.Name));
    }

    private static MemberReferenceHandle FindTypeSpecificationMemberReference(MetadataReader reader, string memberName)
    {
        foreach (var handle in reader.MemberReferences)
        {
            var memberReference = reader.GetMemberReference(handle);
            if ((memberReference.Parent.Kind == HandleKind.TypeSpecification) &&
                (reader.GetString(memberReference.Name) == memberName))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not find TypeSpecification member reference '{memberName}'.");
    }
}

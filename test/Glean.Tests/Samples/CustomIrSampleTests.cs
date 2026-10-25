using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using CustomIrSample;
using Xunit;

using SampleProgram = CustomIrSample.Program;

using Glean.Contexts;
using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Samples;

public sealed class CustomIrSampleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecodeMethodSignature_GenericParameters_RemainFormalOrUseSeparateContexts(bool substitute)
    {
        using var fixture = new SignatureFixture([0x10, 0x02, 0x02, 0x1e, 0x00, 0x13, 0x00, 0x1e, 0x01]);
        var provider = new IrTypeProvider();
        var context = substitute
            ? new IrGenericContext([new IrType.Primitive(PrimitiveTypeCode.Byte)],
                [new IrType.Primitive(PrimitiveTypeCode.Boolean), new IrType.Primitive(PrimitiveTypeCode.String)])
            : default;

        var signature = fixture.Method.DecodeSignature(provider, context);

        Assert.Equal(2, signature.GenericParameterCount);
        if (substitute)
        {
            Primitive(signature.ReturnType, PrimitiveTypeCode.Boolean);
            Primitive(signature.ParameterTypes[0], PrimitiveTypeCode.Byte);
            Primitive(signature.ParameterTypes[1], PrimitiveTypeCode.String);
        }
        else
        {
            Assert.Equal(new IrType.GenericParameter(0, true), signature.ReturnType);
            Assert.Equal(new IrType.GenericParameter(0, false), signature.ParameterTypes[0]);
            Assert.Equal(new IrType.GenericParameter(1, true), signature.ParameterTypes[1]);
        }
    }

    [Fact]
    public void DecodeMethodSignature_ArrayVectorPointerAndByReference_PreserveDistinctShapes()
    {
        using var fixture = new SignatureFixture([0x00, 0x04, 0x01,
            0x1d, 0x08,
            0x14, 0x08, 0x02, 0x02, 0x03, 0x04, 0x02, 0x7f, 0x04,
            0x0f, 0x08,
            0x10, 0x08]);

        var signature = fixture.Method.DecodeSignature(new IrTypeProvider(), default(IrGenericContext));

        Primitive(Assert.IsType<IrType.Vector>(signature.ParameterTypes[0]).Element, PrimitiveTypeCode.Int32);
        var array = Assert.IsType<IrType.Array>(signature.ParameterTypes[1]);
        Primitive(array.Element, PrimitiveTypeCode.Int32);
        Assert.Equal(2, array.Shape.Rank);
        Assert.Equal(new[] { 3, 4 }, array.Shape.Sizes);
        Assert.Equal(new[] { -1, 2 }, array.Shape.LowerBounds);
        var pointer = Assert.IsType<IrType.Wrapped>(signature.ParameterTypes[2]);
        Assert.Equal(Wrapper.Pointer, pointer.Kind);
        Primitive(pointer.Element, PrimitiveTypeCode.Int32);
        var reference = Assert.IsType<IrType.Wrapped>(signature.ParameterTypes[3]);
        Assert.Equal(Wrapper.ByReference, reference.Kind);
        Primitive(reference.Element, PrimitiveTypeCode.Int32);
    }

    [Fact]
    public void GetTypeFromSpecification_UsesCustomNodesAndContextThroughoutNestedGenericTypes()
    {
        using var fixture = new SignatureFixture([0x00, 0x00, 0x01]);
        var provider = new IrTypeProvider();
        var context = new IrGenericContext([new IrType.Primitive(PrimitiveTypeCode.Int32)],
            [new IrType.Primitive(PrimitiveTypeCode.String)]);

        var type = provider.GetTypeFromSpecification(fixture.Scope.Reader, context, fixture.Specification, 0x12);

        var instance = Assert.IsType<IrType.GenericInstance>(type);
        var definition = Assert.IsType<IrType.Named>(instance.Definition);
        Assert.Equal("Fixtures.Shapes`1", definition.Identity.FullName);
        var vector = Assert.IsType<IrType.Vector>(Assert.Single(instance.Arguments));
        Primitive(vector.Element, PrimitiveTypeCode.Int32);
    }

    [Fact]
    public void DecodeLocalSignature_PinnedReference_PreservesBothWrappers()
    {
        using var fixture = new SignatureFixture([0x00, 0x00, 0x01]);

        var locals = fixture.Scope.Reader.GetStandaloneSignature(fixture.Locals)
            .DecodeLocalSignature(new IrTypeProvider(), default(IrGenericContext));

        var pinned = Assert.IsType<IrType.Wrapped>(Assert.Single(locals));
        Assert.Equal(Wrapper.Pinned, pinned.Kind);
        var reference = Assert.IsType<IrType.Wrapped>(pinned.Element);
        Assert.Equal(Wrapper.ByReference, reference.Kind);
        Primitive(reference.Element, PrimitiveTypeCode.Int32);
        Assert.Equal("pinned Int32&", IrDisplay.Type(pinned));
    }

    [Fact]
    public void DecodeMethodSignature_CustomModifiers_PreserveOrderAndRequiredFlags()
    {
        using var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x1f, 0x05, 0x20, 0x09, 0x08]);

        var signature = fixture.Method.DecodeSignature(new IrTypeProvider(), default(IrGenericContext));

        var required = Assert.IsType<IrType.Modified>(Assert.Single(signature.ParameterTypes));
        Assert.True(required.Required);
        Assert.Equal("Fixtures.Outer", Assert.IsType<IrType.Named>(required.Modifier).Identity.FullName);
        var optional = Assert.IsType<IrType.Modified>(required.Unmodified);
        Assert.False(optional.Required);
        Assert.Equal("Fixtures.Outer+Inner", Assert.IsType<IrType.Named>(optional.Modifier).Identity.FullName);
        Primitive(optional.Unmodified, PrimitiveTypeCode.Int32);
    }

    [Fact]
    public void DecodeMethodSignature_FunctionPointer_PreservesHeaderAndSentinelBoundary()
    {
        using var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x1b, 0x65, 0x02, 0x08, 0x0e, 0x41, 0x0f, 0x08]);

        var signature = fixture.Method.DecodeSignature(new IrTypeProvider(), default(IrGenericContext));

        var pointer = Assert.IsType<IrType.FunctionPointer>(Assert.Single(signature.ParameterTypes));
        Assert.Equal(0x65, pointer.Signature.Header.RawValue);
        Assert.Equal(1, pointer.Signature.RequiredParameterCount);
        Assert.Equal(2, pointer.Signature.ParameterTypes.Length);
        Assert.Equal("fnptr VarArgs instance explicitthis<0> Int32(String, ..., Int32*)", IrDisplay.Type(pointer));
    }

    [Fact]
    public void DecodeMethodSignature_UnresolvedNestedName_PreservesIdentityAfterScopesClose()
    {
        IrType.Named named;
        Guid origin;
        using (var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x11, 0x09]))
        {
            using var set = new AssemblySet();
            var signature = fixture.Method.DecodeSignature(new IrTypeProvider(set), default(IrGenericContext));
            named = Assert.IsType<IrType.Named>(Assert.Single(signature.ParameterTypes));
            origin = fixture.Scope.Reader.GetGuid(fixture.Scope.Reader.GetModuleDefinition().Mvid);
        }

        Assert.Equal("Fixtures.Outer+Inner", named.Identity.FullName);
        Assert.Equal("External, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", named.Identity.Scope);
        Assert.Equal(origin, named.Identity.OriginModule);
        Assert.Equal(0x01000002, named.Identity.Token);
        Assert.Equal(0x11, named.RawTypeKind);
        Assert.Equal(ResolutionFailureReason.AssemblyNotFound, named.Failure);
        Assert.Null(named.Definition);
        Assert.Contains("AssemblyNotFound", IrDisplay.Type(named));
    }

    [Fact]
    public void DecodeMethodSignature_NamedResolution_PreservesReferenceAndResolvedDefinition()
    {
        using var workspace = new TemporaryDirectory();
        var dependencyPath = TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "External",
            "[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")] namespace Fixtures; public class Outer { public class Inner { } }");
        using var dependency = new MetadataScope(dependencyPath);
        using var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x12, 0x09]);
        using var set = new AssemblySet();
        set.Add(dependency.Reader);

        var signature = fixture.Method.DecodeSignature(new IrTypeProvider(set), default(IrGenericContext));

        var named = Assert.IsType<IrType.Named>(Assert.Single(signature.ParameterTypes));
        Assert.NotNull(named.Definition);
        Assert.Equal("Fixtures.Outer+Inner", named.Definition.FullName);
        Assert.Equal(named.Identity.FullName, named.Definition.FullName);
        Assert.Equal(0x01000002, named.Identity.Token);
        Assert.Equal(0x02000000, named.Definition.Token & unchecked((int)0xff000000));
        Assert.Equal(ResolutionFailureReason.None, named.Failure);
    }

    [Fact]
    public void MethodReport_SparseUnorderedParameterRows_ProducesStablePositionBasedOutput()
    {
        using var fixture = new SignatureFixture([0x00, 0x03, 0x08, 0x08, 0x0e, 0x10, 0x08],
            (3, "third", ParameterAttributes.Out), (0, "result", ParameterAttributes.Retval), (1, "", ParameterAttributes.None));
        using var writer = Writer();

        MethodReport.Write(fixture.Method, new IrTypeProvider(), writer);

        Assert.Equal("""
            Method: Inspect Default<0> Int32(Int32, String, Int32&)
              return: Int32 [seq=0, name=result, flags=Retval]
              parameter[0]: Int32 [seq=1, name=<unnamed>, flags=None]
              parameter[1]: String [no metadata row]
              parameter[2]: Int32& [seq=3, name=third, flags=Out]

            """, writer.ToString());
    }

    [Fact]
    public void Program_WithoutResolution_ProducesDeterministicCustomModelOutput()
    {
        using var workspace = new TemporaryDirectory();
        using var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x08]);
        var path = Path.Combine(workspace.Path, "Fixture.dll");
        File.WriteAllBytes(path, fixture.Image);
        using var output = Writer();
        using var error = Writer();

        int exitCode = SampleProgram.Run([path, "Fixtures.Shapes`1"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal("""
            Type: Fixtures.Shapes`1
            Method: Inspect Default<0> Void(Int32)
              return: Void [no metadata row]
              parameter[0]: Int32 [no metadata row]

            """, output.ToString());
    }

    [Fact]
    public void Program_WithMissingDependencies_PrintsUnresolvedIdentityInsteadOfFailing()
    {
        using var workspace = new TemporaryDirectory();
        using var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x12, 0x09]);
        var path = Path.Combine(workspace.Path, "Fixture.dll");
        File.WriteAllBytes(path, fixture.Image);
        using var output = Writer();
        using var error = Writer();

        int exitCode = SampleProgram.Run([path, "--resolve"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("Fixtures.Outer+Inner [unresolved: External, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null; AssemblyNotFound]", output.ToString());
    }

    [Fact]
    public void Program_WithDependencySearch_ResolvesNamedLeavesThroughClosure()
    {
        using var workspace = new TemporaryDirectory();
        TestAssemblyFiles.BuildAssemblyFile(workspace.Path, "External",
            "[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")] namespace Fixtures; public class Outer { public class Inner { } }");
        using var fixture = new SignatureFixture([0x00, 0x01, 0x01, 0x12, 0x09]);
        var path = Path.Combine(workspace.Path, "Fixture.dll");
        File.WriteAllBytes(path, fixture.Image);
        using var output = Writer();
        using var error = Writer();

        int exitCode = SampleProgram.Run([path, "--search", workspace.Path], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("Void(Fixtures.Outer+Inner)", output.ToString());
        Assert.DoesNotContain("unresolved:", output.ToString());
    }

    [Theory]
    [InlineData("--search")]
    [InlineData("--unknown")]
    public void Program_InvalidOption_ReportsUsageBeforeOpeningFile(string option)
    {
        using var output = Writer();
        using var error = Writer();

        Assert.Equal(1, SampleProgram.Run(["missing.dll", option], output, error));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Usage:", error.ToString());
    }

    private static void Primitive(IrType type, PrimitiveTypeCode expected)
    {
        Assert.Equal(expected, Assert.IsType<IrType.Primitive>(type).Code);
    }

    private static StringWriter Writer() => new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    private sealed class SignatureFixture : IDisposable
    {
        public SignatureFixture(byte[] signature, params (int Sequence, string Name, ParameterAttributes Attributes)[] parameters)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(0, metadata.GetOrAddString("Fixture.dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
            metadata.AddAssembly(metadata.GetOrAddString("Fixture"), new Version(1, 0, 0, 0), default, default, 0, AssemblyHashAlgorithm.None);
            var external = metadata.AddAssemblyReference(metadata.GetOrAddString("External"), new Version(1, 0, 0, 0), default, default, 0, default);
            var outer = metadata.AddTypeReference(external, metadata.GetOrAddString("Fixtures"), metadata.GetOrAddString("Outer"));
            metadata.AddTypeReference(outer, default, metadata.GetOrAddString("Inner"));
            metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"), default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            var type = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixtures"),
                metadata.GetOrAddString("Shapes`1"), default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            var method = metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
                metadata.GetOrAddString("Inspect"), metadata.GetOrAddBlob(signature), -1, MetadataTokens.ParameterHandle(1));
            foreach (var parameter in parameters)
            {
                metadata.AddParameter(parameter.Attributes, metadata.GetOrAddString(parameter.Name), parameter.Sequence);
            }

            if ((signature[0] & 0x10) != 0)
            {
                for (int i = 0; i < signature[1]; i++)
                {
                    metadata.AddGenericParameter(method, GenericParameterAttributes.None, metadata.GetOrAddString("M" + i), i);
                }
            }

            metadata.AddGenericParameter(type, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
            Specification = metadata.AddTypeSpecification(metadata.GetOrAddBlob(new byte[] { 0x15, 0x12, 0x08, 0x01, 0x1d, 0x13, 0x00 }));
            Locals = metadata.AddStandaloneSignature(metadata.GetOrAddBlob(new byte[] { 0x07, 0x01, 0x45, 0x10, 0x08 }));
            var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(), flags: CorFlags.ILOnly);
            var image = new BlobBuilder();
            pe.Serialize(image);
            Image = image.ToArray();
            Scope = new MetadataScope(new MemoryStream(Image, writable: false));
            Method = MethodContext.Create(Scope.Reader, method);
        }

        public byte[] Image { get; }
        public MetadataScope Scope { get; }
        public MethodContext Method { get; }
        public TypeSpecificationHandle Specification { get; }
        public StandaloneSignatureHandle Locals { get; }
        public void Dispose() => Scope.Dispose();
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

using Xunit;

using Glean.Contexts;
using Glean.Enumerators;

namespace Glean.Tests.Contexts;

public sealed class MethodDebugAccessTests
{
    private const string DebugFixtureSource = """
        using System;

        public static class MethodDebugFixture
        {
            public static int Compute(int value)
            {
                int total = value + 1000;
                if (total > 1000)
                {
                    total++;
                }

                return total;
            }
        }
        """;

    [Fact]
    public void EnumerateSequencePoints_ReturnsVisiblePointsAndDocument()
    {
        using var metadata = PortableDebugMetadataScope.Build(
            DebugFixtureSource,
            "/src/MethodDebugFixture.cs");

        var method = metadata.GetMethodContext("MethodDebugFixture", "Compute");
        var visibleSequencePoints = new List<SequencePoint>();

        foreach (var sequencePoint in method.EnumerateSequencePoints(metadata.PdbReader))
        {
            if (!sequencePoint.IsHidden)
            {
                visibleSequencePoints.Add(sequencePoint);
            }
        }

        Assert.NotEmpty(visibleSequencePoints);
        Assert.False(visibleSequencePoints[0].Document.IsNil);

        var document = DocumentContext.Create(metadata.PdbReader, visibleSequencePoints[0].Document);

        Assert.EndsWith("MethodDebugFixture.cs", document.Name);
        Assert.NotEqual(Guid.Empty, document.Language);
    }

    [Fact]
    public void EnumerateILInstructions_MatchesSpanEnumerator()
    {
        using var metadata = PortableDebugMetadataScope.Build(
            DebugFixtureSource,
            "/src/MethodDebugFixture.cs");

        var method = metadata.GetMethodContext("MethodDebugFixture", "Compute");
        var instructions = new List<ILInstruction>();
        var spanInstructions = new List<ILInstruction>();

        foreach (var instruction in method.EnumerateILInstructions(metadata.PEReader))
        {
            instructions.Add(instruction);
        }

        foreach (var instruction in method.EnumerateILInstructionsFromSpan(metadata.PEReader))
        {
            spanInstructions.Add(instruction);
        }

        Assert.NotEmpty(instructions);
        Assert.Equal(instructions.Count, spanInstructions.Count);

        for (int i = 0; i < instructions.Count; i++)
        {
            Assert.Equal(instructions[i].Offset, spanInstructions[i].Offset);
            Assert.Equal(instructions[i].OpCode, spanInstructions[i].OpCode);
            Assert.Equal(instructions[i].OperandToken, spanInstructions[i].OperandToken);
            Assert.Equal(instructions[i].OperandKind, spanInstructions[i].OperandKind);
        }

        Assert.Contains(
            instructions,
            instruction => (instruction.OperandKind == ILOperandKind.Int32) && (instruction.OperandToken == 1000));
    }

    [Fact]
    public void ILInstructionEnumerators_SupportedOperands_PreserveOffsetsAndValues()
    {
        byte[] il =
        {
            0x00,
            0x1F, 0xF9,
            0x20, 0x78, 0x56, 0x34, 0x12,
            0x2B, 0xFE,
            0xFE, 0x09, 0x02, 0x01,
            0x28, 0x01, 0x00, 0x00, 0x06,
            0x2A
        };
        var expected = new (int Offset, ILOpCode OpCode, int Operand, ILOperandKind Kind)[]
        {
            (0, ILOpCode.Nop, 0, ILOperandKind.None),
            (1, ILOpCode.Ldc_i4_s, -7, ILOperandKind.Int32),
            (3, ILOpCode.Ldc_i4, 0x12345678, ILOperandKind.Int32),
            (8, ILOpCode.Br_s, -2, ILOperandKind.BranchTarget),
            (10, ILOpCode.Ldarg, 258, ILOperandKind.LocalOrArg),
            (14, ILOpCode.Call, 0x06000001, ILOperandKind.MetadataToken),
            (19, ILOpCode.Ret, 0, ILOperandKind.None)
        };
        var copied = ILInstructionEnumerator.Create(il);
        var span = ILInstructionSpanEnumerator.Create(il);

        foreach (var instruction in expected)
        {
            Assert.True(copied.MoveNext());
            Assert.True(span.MoveNext());
            AssertInstruction(instruction, copied.Current);
            AssertInstruction(instruction, span.Current);
        }

        Assert.False(copied.MoveNext());
        Assert.False(span.MoveNext());
    }

    [Fact]
    public void ExperimentalIlApis_AreNotPublic()
    {
        var exported = typeof(MethodContext).Assembly.GetExportedTypes();

        Assert.DoesNotContain(typeof(ILInstruction), exported);
        Assert.DoesNotContain(typeof(ILInstructionEnumerator), exported);
        Assert.DoesNotContain(typeof(ILInstructionSpanEnumerator), exported);
        Assert.DoesNotContain(typeof(ILOperandKind), exported);
        Assert.Null(typeof(MethodContext).GetMethod(nameof(MethodContext.EnumerateILInstructions)));
        Assert.Null(typeof(MethodContext).GetMethod(nameof(MethodContext.EnumerateILInstructionsFromSpan)));
    }

    [Fact]
    public void PortablePdbApis_RemainPublic()
    {
        var exported = typeof(MethodContext).Assembly.GetExportedTypes();

        Assert.Contains(typeof(DocumentContext), exported);
        Assert.Contains(typeof(SequencePointEnumerator), exported);
        Assert.NotNull(typeof(MethodContext).GetMethod(nameof(MethodContext.EnumerateSequencePoints)));
    }

    private static void AssertInstruction(
        (int Offset, ILOpCode OpCode, int Operand, ILOperandKind Kind) expected,
        ILInstruction actual)
    {
        Assert.Equal(expected.Offset, actual.Offset);
        Assert.Equal(expected.OpCode, actual.OpCode);
        Assert.Equal(expected.Operand, actual.OperandToken);
        Assert.Equal(expected.Kind, actual.OperandKind);
    }

    private sealed class PortableDebugMetadataScope : IDisposable
    {
        private readonly MemoryStream _peStream;
        private readonly MemoryStream _pdbStream;
        private readonly PEReader _peReader;
        private readonly MetadataReaderProvider _pdbProvider;

        private PortableDebugMetadataScope(
            MemoryStream peStream,
            MemoryStream pdbStream,
            PEReader peReader,
            MetadataReaderProvider pdbProvider)
        {
            _peStream = peStream;
            _pdbStream = pdbStream;
            _peReader = peReader;
            _pdbProvider = pdbProvider;
            Reader = peReader.GetMetadataReader();
            PdbReader = pdbProvider.GetMetadataReader();
        }

        public MetadataReader Reader { get; }

        public MetadataReader PdbReader { get; }

        public PEReader PEReader => _peReader;

        public static PortableDebugMetadataScope Build(string source, string documentPath)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(documentPath);

            var syntaxTree = CSharpSyntaxTree.ParseText(
                SourceText.From(source, Encoding.UTF8),
                path: documentPath);
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName: "MethodDebugFixtureAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug));

            var peStream = new MemoryStream();
            var pdbStream = new MemoryStream();
            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
            var emitResult = compilation.Emit(peStream, pdbStream: pdbStream, options: emitOptions);

            if (!emitResult.Success)
            {
                peStream.Dispose();
                pdbStream.Dispose();

                string errors = string.Join(
                    Environment.NewLine,
                    emitResult.Diagnostics
                        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                        .Select(diagnostic => diagnostic.ToString()));

                throw new InvalidOperationException($"Compilation failed:{Environment.NewLine}{errors}");
            }

            peStream.Position = 0;
            pdbStream.Position = 0;

            var peReader = new PEReader(peStream);
            var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(
                pdbStream,
                MetadataStreamOptions.LeaveOpen);

            return new PortableDebugMetadataScope(peStream, pdbStream, peReader, pdbProvider);
        }

        public MethodContext GetMethodContext(string typeName, string methodName)
        {
            foreach (var typeHandle in Reader.TypeDefinitions)
            {
                var typeDefinition = Reader.GetTypeDefinition(typeHandle);
                if (!Reader.StringComparer.Equals(typeDefinition.Name, typeName))
                {
                    continue;
                }

                foreach (var methodHandle in typeDefinition.GetMethods())
                {
                    var methodDefinition = Reader.GetMethodDefinition(methodHandle);
                    if (Reader.StringComparer.Equals(methodDefinition.Name, methodName))
                    {
                        return MethodContext.Create(Reader, methodHandle);
                    }
                }
            }

            throw new InvalidOperationException($"Could not locate method {typeName}.{methodName}.");
        }

        public void Dispose()
        {
            _pdbProvider.Dispose();
            _peReader.Dispose();
            _pdbStream.Dispose();
            _peStream.Dispose();
        }
    }
}

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

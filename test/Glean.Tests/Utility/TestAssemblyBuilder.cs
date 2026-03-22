using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Glean.Tests.Utility;

internal sealed class TestAssemblyBuilder
{
    private readonly string _assemblyName;
    private readonly List<string> _sources = new();
    private readonly List<MetadataReference> _references = new();
    private readonly List<ResourceDescription> _manifestResources = new();
    private bool _allowUnsafe;
    private OutputKind _outputKind = OutputKind.DynamicallyLinkedLibrary;

    internal TestAssemblyBuilder(string assemblyName = "TestAssembly")
    {
        _assemblyName = assemblyName;

        _references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        _references.Add(MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
        _references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
    }

    internal TestAssemblyBuilder WithSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _sources.Add(source);
        return this;
    }

    internal TestAssemblyBuilder WithUnsafe(bool allowUnsafe = true)
    {
        _allowUnsafe = allowUnsafe;
        return this;
    }

    internal TestAssemblyBuilder WithOutputKind(OutputKind kind)
    {
        _outputKind = kind;
        return this;
    }

    internal TestAssemblyBuilder WithReference(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        _references.Add(MetadataReference.CreateFromFile(assembly.Location));
        return this;
    }

    internal TestAssemblyBuilder WithReference(string assemblyPath)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);

        _references.Add(MetadataReference.CreateFromFile(assemblyPath));
        return this;
    }

    internal TestAssemblyBuilder WithReference(string assemblyPath, params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(assemblyPath);
        ArgumentNullException.ThrowIfNull(aliases);

        MetadataReferenceProperties properties = MetadataReferenceProperties.Assembly;
        if (aliases.Length > 0)
        {
            properties = properties.WithAliases(ImmutableArray.CreateRange(aliases));
        }

        _references.Add(MetadataReference.CreateFromFile(assemblyPath, properties: properties));
        return this;
    }

    internal TestAssemblyBuilder WithModuleReference(string modulePath)
    {
        ArgumentNullException.ThrowIfNull(modulePath);

        _references.Add(MetadataReference.CreateFromFile(
            modulePath,
            properties: MetadataReferenceProperties.Module));
        return this;
    }

    internal TestAssemblyBuilder WithManifestResource(string name, byte[] data, bool isPublic = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(data);

        _manifestResources.Add(new ResourceDescription(
            name,
            () => new MemoryStream(data, writable: false),
            isPublic));

        return this;
    }

    internal MetadataScope BuildMetadataScope()
    {
        return new MetadataScope(BuildPEStream());
    }

    internal MemoryStream BuildPEStream()
    {
        var syntaxTrees = _sources
            .Select(source => CSharpSyntaxTree.ParseText(source))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            _assemblyName,
            syntaxTrees,
            _references,
            new CSharpCompilationOptions(
                _outputKind,
                allowUnsafe: _allowUnsafe,
                optimizationLevel: OptimizationLevel.Release));

        var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream, manifestResources: _manifestResources);
        if (!emitResult.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString()));

            throw new InvalidOperationException($"Compilation failed:{Environment.NewLine}{errors}");
        }

        peStream.Position = 0;
        return peStream;
    }

    internal unsafe PinnedBuffer BuildUnsafePointer()
    {
        var peStream = BuildPEStream();
        var peReader = new PEReader(peStream);
        var reader = peReader.GetMetadataReader();
        var metadata = peReader.GetMetadata();

        return new PinnedBuffer(peStream, peReader, reader, metadata.Pointer, metadata.Length);
    }

    internal sealed class PinnedBuffer : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly PEReader _peReader;

        internal unsafe PinnedBuffer(
            MemoryStream stream,
            PEReader peReader,
            MetadataReader reader,
            byte* pointer,
            int length)
        {
            _stream = stream;
            _peReader = peReader;
            Reader = reader;
            Pointer = pointer;
            Length = length;
        }

        internal unsafe byte* Pointer { get; }

        internal int Length { get; }

        internal MetadataReader Reader { get; }

        public void Dispose()
        {
            _peReader.Dispose();
            _stream.Dispose();
        }
    }
}

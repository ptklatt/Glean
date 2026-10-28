using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FocusedTraversalBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        if (RawFlags() != ContextFlags() || RawFlags() != ContextCachedFlags() || RawParameters() != ContextParameters())
        {
            throw new InvalidOperationException("Isolated workload mismatch");
        }
        Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256}");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.FocusedFlags")]
    public long RawFlags()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            var attributes = type.Attributes;
            sum += (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public ? 1 : 0;
            sum += (attributes & TypeAttributes.Interface) != 0 ? 2 : 0;
            sum += (attributes & TypeAttributes.Abstract) != 0 ? 4 : 0;
            sum += (attributes & TypeAttributes.Sealed) != 0 ? 8 : 0;
            foreach (var handle in type.GetMethods())
            {
                var flags = reader.GetMethodDefinition(handle).Attributes;
                var access = flags & MethodAttributes.MemberAccessMask;
                sum += access == MethodAttributes.Public ? 1 : 0;
                sum += access == MethodAttributes.Private ? 2 : 0;
                sum += (flags & MethodAttributes.Static) != 0 ? 4 : 0;
                sum += (flags & MethodAttributes.Virtual) != 0 ? 8 : 0;
                sum += (flags & MethodAttributes.Abstract) != 0 ? 16 : 0;
                sum += (flags & MethodAttributes.Final) != 0 ? 32 : 0;
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.FocusedFlags")]
    public long ContextFlags()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            sum += type.IsPublic ? 1 : 0;
            sum += type.IsInterface ? 2 : 0;
            sum += type.IsAbstract ? 4 : 0;
            sum += type.IsSealed ? 8 : 0;
            foreach (var method in type.EnumerateMethods())
            {
                sum += method.IsPublic ? 1 : 0;
                sum += method.IsPrivate ? 2 : 0;
                sum += method.IsStatic ? 4 : 0;
                sum += method.IsVirtual ? 8 : 0;
                sum += method.IsAbstract ? 16 : 0;
                sum += method.IsFinal ? 32 : 0;
            }
        }
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.FocusedParameters")]
    public long RawParameters()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            foreach (var method in reader.GetTypeDefinition(handle).GetMethods())
            {
                foreach (var parameter in reader.GetMethodDefinition(method).GetParameters())
                {
                    var value = reader.GetParameter(parameter);
                    sum += MetadataTokens.GetRowNumber(parameter) + value.SequenceNumber + (int)value.Attributes;
                }
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.FocusedFlags")]
    public long ContextCachedFlags()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            var attributes = type.Attributes;
            sum += (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public ? 1 : 0;
            sum += (attributes & TypeAttributes.Interface) != 0 ? 2 : 0;
            sum += (attributes & TypeAttributes.Abstract) != 0 ? 4 : 0;
            sum += (attributes & TypeAttributes.Sealed) != 0 ? 8 : 0;
            foreach (var method in type.EnumerateMethods())
            {
                var flags = method.Attributes;
                var access = flags & MethodAttributes.MemberAccessMask;
                sum += access == MethodAttributes.Public ? 1 : 0;
                sum += access == MethodAttributes.Private ? 2 : 0;
                sum += (flags & MethodAttributes.Static) != 0 ? 4 : 0;
                sum += (flags & MethodAttributes.Virtual) != 0 ? 8 : 0;
                sum += (flags & MethodAttributes.Abstract) != 0 ? 16 : 0;
                sum += (flags & MethodAttributes.Final) != 0 ? 32 : 0;
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.FocusedParameters")]
    public long ContextParameters()
    {
        long sum = 0;
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            foreach (var method in type.EnumerateMethods())
            {
                foreach (var parameter in method.EnumerateParameters())
                {
                    sum += MetadataTokens.GetRowNumber(parameter.Handle) + parameter.SequenceNumber + (int)parameter.Attributes;
                }
            }
        }
        return sum;
    }

    public void Dispose() => _corpus.Dispose();
}

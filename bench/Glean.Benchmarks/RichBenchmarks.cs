using BenchmarkDotNet.Attributes;

using Glean.Contexts;
using Glean.Providers;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[BenchmarkCategory("Rich.SignatureDecoding")]
[JsonExporterAttribute.Full]
public class RichBenchmarks : IDisposable
{
    private MetadataFixture _fixture = null!;
    private AssemblyContext _assembly;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MetadataFixture();
        _assembly = AssemblyContext.Create(_fixture.Reader);
        MetadataFixture.Require(SrmDecodeWithGleanProvider() == GleanDecode(), "Rich decode equivalence");
        MetadataFixture.Require(GleanDecode() == MetadataFixture.TypeCount * (2 * 2 + 6 * 3), "Rich decode checksum");
    }

    [Benchmark(Baseline = true)]
    public int SrmDecodeWithGleanProvider()
    {
        int checksum = 0;
        foreach (var typeHandle in _fixture.Reader.TypeDefinitions)
        {
            var type = _fixture.Reader.GetTypeDefinition(typeHandle);
            foreach (var handle in type.GetMethods())
            {
                var definition = _fixture.Reader.GetMethodDefinition(handle);
                var signature = definition.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                checksum += signature.ParameterTypes.Length + (signature.ReturnType == null ? 0 : 1);
            }
        }

        return checksum;
    }

    [Benchmark]
    public int GleanDecode()
    {
        int checksum = 0;
        foreach (var type in _assembly.EnumerateTypes())
        {
            foreach (var method in type.EnumerateMethods())
            {
                var signature = method.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                checksum += signature.ParameterTypes.Length + (signature.ReturnType == null ? 0 : 1);
            }
        }

        return checksum;
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _fixture?.Dispose();
    }
}

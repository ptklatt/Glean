using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

using Glean.Benchmarks.Fixture;
using Glean.Contexts;

namespace Glean.Benchmarks;

internal sealed class CorpusFixture : IDisposable
{
    private readonly PEReader _pe;

    public CorpusFixture(string corpus)
    {
        Path = corpus switch
        {
            "Fixture" => Environment.GetEnvironmentVariable("GLEAN_BENCHMARK_FIXTURE_PATH") ?? typeof(Leaf).Assembly.Location,
            "Linq" => System.IO.Path.Combine(RuntimeDirectory, "System.Linq.dll"),
            "CoreLib" => System.IO.Path.Combine(RuntimeDirectory, "System.Private.CoreLib.dll"),
            _ => throw new ArgumentOutOfRangeException(nameof(corpus))
        };
        byte[] image = File.ReadAllBytes(Path);
        Sha256 = Convert.ToHexString(SHA256.HashData(image));
        _pe = new PEReader(new MemoryStream(image, writable: false));
        Reader = _pe.GetMetadataReader();
        Assembly = AssemblyContext.Create(Reader);
    }

    public string Path { get; }
    public static string RuntimeDirectory => Environment.GetEnvironmentVariable("GLEAN_BENCHMARK_RUNTIME_DIRECTORY")
        ?? System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
    public string Sha256 { get; }
    public MetadataReader Reader { get; }
    public AssemblyContext Assembly { get; }

    public void Dispose() => _pe.Dispose();
}

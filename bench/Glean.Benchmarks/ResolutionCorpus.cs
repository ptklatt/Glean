using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Glean.Resolution;

namespace Glean.Benchmarks;

internal sealed class ResolutionCorpus : IDisposable
{
    private readonly CorpusFixture? _core;
    private readonly PEReader _facade;
    private readonly MetadataReader _facadeReader;

    public ResolutionCorpus(string corpus)
    {
        Source = new CorpusFixture(corpus);
        if (corpus != "CoreLib")
        {
            _core = new CorpusFixture("CoreLib");
        }
        string runtime = CorpusFixture.RuntimeDirectory;
        _facade = new PEReader(File.OpenRead(System.IO.Path.Combine(runtime, "System.Runtime.dll")));
        _facadeReader = _facade.GetMetadataReader();
    }

    public CorpusFixture Source { get; }

    public AssemblySet CreateSet()
    {
        var set = new AssemblySet();
        set.Add(Source.Reader);
        if (_core != null)
        {
            set.Add(_core.Reader);
        }
        set.Add(_facadeReader);
        return set;
    }

    public int ReaderIdentity(MetadataReader reader)
        => ReferenceEquals(reader, Source.Reader) ? 1 : ReferenceEquals(reader, _core?.Reader) ? 2 :
            ReferenceEquals(reader, _facadeReader) ? 3 : throw new InvalidOperationException("Unexpected resolved reader.");

    public void Dispose()
    {
        _facade.Dispose();
        _core?.Dispose();
        Source.Dispose();
    }
}

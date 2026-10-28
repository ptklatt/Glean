using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Contexts;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CompoundFilterBenchmarks : IDisposable
{
    private MetadataFixture? _synthetic;
    private CorpusFixture? _corpus;
    private MetadataReader _reader = null!;
    private AssemblyContext _assembly;
    private string _namespace = "";
    private string _publicName = "";
    private string _privateName = "";

    [Params("Synthetic", "Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Synthetic";

    [GlobalSetup]
    public void Setup()
    {
        if (Corpus == "Synthetic")
        {
            _synthetic = new MetadataFixture();
            _reader = _synthetic.Reader;
            _namespace = MetadataFixture.TargetNamespace;
            _publicName = _privateName = "Echo";
        }
        else
        {
            _corpus = new CorpusFixture(Corpus);
            _reader = _corpus.Reader;
            _namespace = Corpus switch { "Fixture" => "Glean.Benchmarks.Fixture", "Linq" => "System.Linq", _ => "System" };
            foreach (var handle in _reader.TypeDefinitions)
            {
                var type = _reader.GetTypeDefinition(handle);
                if (((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) ||
                    !_reader.StringComparer.Equals(type.Namespace, _namespace))
                {
                    continue;
                }
                foreach (var methodHandle in type.GetMethods())
                {
                    var method = _reader.GetMethodDefinition(methodHandle);
                    var access = method.Attributes & MethodAttributes.MemberAccessMask;
                    if ((access == MethodAttributes.Public) && (_publicName.Length == 0))
                    {
                        _publicName = _reader.GetString(method.Name);
                    }
                    if ((access == MethodAttributes.Private) && (_privateName.Length == 0))
                    {
                        _privateName = _reader.GetString(method.Name);
                    }
                }
            }
            Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256}");
        }
        _assembly = AssemblyContext.Create(_reader);
        foreach (var type in _assembly.EnumerateTypes())
        {
            foreach (var method in type.EnumerateMethods())
            {
                var attributes = _reader.GetMethodDefinition(method.Handle).Attributes;
                MetadataFixture.Require(method.IsPublic == ((attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public), "Public predicate");
                MetadataFixture.Require(method.IsPrivate == ((attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private), "Private predicate");
            }
        }
        MetadataFixture.Require((_publicName.Length > 0) && (_privateName.Length > 0), "Both filter outcomes represented");
        MetadataFixture.Require((SrmPublic() > 0) && (SrmPublic() == GleanPublic()), "Public compound filter");
        MetadataFixture.Require((SrmPrivate() > 0) && (SrmPrivate() == GleanPrivate()), "Private compound filter");
        Console.WriteLine($"Namespace={_namespace} Public={_publicName} Private={_privateName}");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.Filter.Public")]
    public int SrmPublic()
    {
        int sum = 0;
        foreach (var handle in _reader.TypeDefinitions)
        {
            var type = _reader.GetTypeDefinition(handle);
            if (((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) ||
                !_reader.StringComparer.Equals(type.Namespace, _namespace))
            {
                continue;
            }
            foreach (var methodHandle in type.GetMethods())
            {
                var method = _reader.GetMethodDefinition(methodHandle);
                if (((method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public) &&
                    _reader.StringComparer.Equals(method.Name, _publicName))
                {
                    sum += MetadataTokens.GetRowNumber(methodHandle);
                }
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.Filter.Public")]
    public int GleanPublic()
    {
        int sum = 0;
        foreach (var type in _assembly.EnumerateTypes())
        {
            if ((!type.IsPublic) || !type.NamespaceIs(_namespace))
            {
                continue;
            }
            foreach (var method in type.EnumerateMethods())
            {
                if ((method.IsPublic) && method.NameIs(_publicName))
                {
                    sum += MetadataTokens.GetRowNumber(method.Handle);
                }
            }
        }
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Spectrum.Filter.Private")]
    public int SrmPrivate()
    {
        int sum = 0;
        foreach (var handle in _reader.TypeDefinitions)
        {
            var type = _reader.GetTypeDefinition(handle);
            if (((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) ||
                !_reader.StringComparer.Equals(type.Namespace, _namespace))
            {
                continue;
            }
            foreach (var methodHandle in type.GetMethods())
            {
                var method = _reader.GetMethodDefinition(methodHandle);
                if (((method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private) &&
                    _reader.StringComparer.Equals(method.Name, _privateName))
                {
                    sum += MetadataTokens.GetRowNumber(methodHandle);
                }
            }
        }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Spectrum.Filter.Private")]
    public int GleanPrivate()
    {
        int sum = 0;
        foreach (var type in _assembly.EnumerateTypes())
        {
            if ((!type.IsPublic) || !type.NamespaceIs(_namespace))
            {
                continue;
            }
            foreach (var method in type.EnumerateMethods())
            {
                if ((method.IsPrivate) && method.NameIs(_privateName))
                {
                    sum += MetadataTokens.GetRowNumber(method.Handle);
                }
            }
        }
        return sum;
    }

    public void Dispose()
    {
        _corpus?.Dispose();
        _synthetic?.Dispose();
    }
}

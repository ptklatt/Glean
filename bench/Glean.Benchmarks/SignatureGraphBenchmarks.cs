using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Signatures;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.SignatureGraphs")]
public class SignatureGraphBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;
    private CorpusFixture _otherCorpus = null!;
    private TypeSignature[] _signatures = null!;
    private TypeSignature[] _equalSignatures = null!;
    private TypeSignature[] _crossReaderSignatures = null!;

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture("Fixture");
        _otherCorpus = new CorpusFixture("Fixture");
        _signatures = CreateSignatures(_corpus.Reader);
        _equalSignatures = CreateSignatures(_corpus.Reader);
        _crossReaderSignatures = CreateSignatures(_otherCorpus.Reader);
        MetadataFixture.Require(_signatures.Select(value => value.Kind).Distinct().Count() == 15, "All signature graph kinds");
        MetadataFixture.Require(Dispatch() == _signatures.Sum(value => (long)value.Kind + 1), "Signature dispatch checksum");
        MetadataFixture.Require(StructuralEquality() == _signatures.Length, "Equal independently constructed graphs");
        MetadataFixture.Require(CrossReaderEquality() == _signatures.Length - 1, "Cross reader type definitions retain reader identity");
        foreach (var signature in _signatures)
        {
            MetadataFixture.Require(signature.ToString().Length > 0, "Formatted signature value");
        }
    }

    [Benchmark]
    public long Dispatch()
    {
        var visitor = new KindVisitor();
        foreach (var signature in _signatures)
        {
            signature.Accept(ref visitor, 1);
        }
        return visitor.Sum;
    }

    [Benchmark]
    public long Format()
    {
        long sum = 0;
        foreach (var signature in _signatures)
        {
            sum += signature.ToString().Length;
        }
        return sum;
    }

    [Benchmark]
    public long StructuralEquality()
    {
        long sum = 0;
        for (int i = 0; i < _signatures.Length; i++)
        {
            sum += _signatures[i].Equals(_equalSignatures[i]) ? 1 : 0;
        }
        return sum;
    }

    [Benchmark]
    public long CrossReaderEquality()
    {
        long sum = 0;
        for (int i = 0; i < _signatures.Length; i++)
        {
            sum += _signatures[i].Equals(_crossReaderSignatures[i]) ? 1 : 0;
        }
        return sum;
    }

    private static TypeSignature[] CreateSignatures(MetadataReader reader)
    {
        var integer = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var graph = new GenericInstanceSignature(new TypeReferenceSignature(reader, MetadataTokens.TypeReferenceHandle(1)),
            ImmutableArray.Create<TypeSignature>(new SZArraySignature(integer), new PointerSignature(integer)));
        return new TypeSignature[]
        {
            new ArraySignature(integer, new ArrayShape(2, ImmutableArray<int>.Empty, ImmutableArray<int>.Empty)),
            new ByRefSignature(graph),
            new FunctionPointerSignature(new MethodSignature<TypeSignature>(default, integer, 1, 0, ImmutableArray.Create<TypeSignature>(graph))),
            graph,
            new GenericMethodParameterSignature(2),
            new GenericTypeParameterSignature(1),
            new ModifiedTypeSignature(integer, ImmutableArray.Create<TypeSignature>(graph), ImmutableArray<TypeSignature>.Empty),
            new PinnedTypeSignature(integer),
            new PointerSignature(graph),
            integer,
            new SerializedTypeNameSignature("Example.Widget"),
            SentinelTypeSignature.Instance,
            new SZArraySignature(graph),
            new TypeDefinitionSignature(reader, MetadataTokens.TypeDefinitionHandle(2)),
            new TypeReferenceSignature(reader, MetadataTokens.TypeReferenceHandle(1))
        };
    }

    private struct KindVisitor : ITypeSignatureVisitor<int>
    {
        public long Sum;
        private void Record(TypeSignature signature, int state) => Sum += (int)signature.Kind + state;
        public void VisitArray(ArraySignature signature, int state) => Record(signature, state);
        public void VisitByRef(ByRefSignature signature, int state) => Record(signature, state);
        public void VisitFunctionPointer(FunctionPointerSignature signature, int state) => Record(signature, state);
        public void VisitGenericInstance(GenericInstanceSignature signature, int state) => Record(signature, state);
        public void VisitGenericMethodParameter(GenericMethodParameterSignature signature, int state) => Record(signature, state);
        public void VisitGenericTypeParameter(GenericTypeParameterSignature signature, int state) => Record(signature, state);
        public void VisitModifiedType(ModifiedTypeSignature signature, int state) => Record(signature, state);
        public void VisitPinnedType(PinnedTypeSignature signature, int state) => Record(signature, state);
        public void VisitPointer(PointerSignature signature, int state) => Record(signature, state);
        public void VisitPrimitive(PrimitiveTypeSignature signature, int state) => Record(signature, state);
        public void VisitSerializedTypeName(SerializedTypeNameSignature signature, int state) => Record(signature, state);
        public void VisitSentinel(SentinelTypeSignature signature, int state) => Record(signature, state);
        public void VisitSZArray(SZArraySignature signature, int state) => Record(signature, state);
        public void VisitTypeDefinition(TypeDefinitionSignature signature, int state) => Record(signature, state);
        public void VisitTypeReference(TypeReferenceSignature signature, int state) => Record(signature, state);
    }

    public void Dispose()
    {
        _otherCorpus.Dispose();
        _corpus.Dispose();
    }
}

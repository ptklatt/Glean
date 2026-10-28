using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Contexts;
using Glean.Visitors;

namespace Glean.Benchmarks;

public enum TraversalProfile
{
    Standard, Methods, MethodAttributes, AssemblyReferences, Signatures, ConstantDebug, All
}

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.Visitors")]
public class TraversalProfileBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [ParamsAllValues]
    public TraversalProfile Profile { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        long expected = Traverse();
        MetadataFixture.Require((expected > 0) && (expected == Traverse()), "Visitor profile result");
    }

    [Benchmark]
    public long Traverse()
    {
        var visitor = new CountingVisitor();
        var reader = _corpus.Reader;
        switch (Profile)
        {
            case TraversalProfile.Standard: MetadataTraversal.Traverse(reader, ref visitor); break;
            case TraversalProfile.Methods: MetadataTraversal.TraverseMethodsOnly(reader, ref visitor); break;
            case TraversalProfile.MethodAttributes: MetadataTraversal.TraverseMethodAttributesOnly(reader, ref visitor); break;
            case TraversalProfile.AssemblyReferences: MetadataTraversal.TraverseWithAssemblyReferences(reader, ref visitor); break;
            case TraversalProfile.Signatures: MetadataTraversal.TraverseWithSignatureTables(reader, ref visitor); break;
            case TraversalProfile.ConstantDebug: MetadataTraversal.TraverseWithConstantAndDebugTables(reader, ref visitor); break;
            case TraversalProfile.All: MetadataTraversal.TraverseAllTables(reader, ref visitor); break;
        }
        return visitor.Sum;
    }

    private struct CountingVisitor : ICompleteMetadataVisitor
    {
        public long Sum;
        public bool VisitType(in TypeContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); return true; }
        public void VisitMethod(in MethodContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitField(in FieldContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitProperty(in PropertyContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitEvent(in EventContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitParameter(in ParameterContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitGenericParameter(in GenericParameterContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitCustomAttribute(in CustomAttributeContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitTypeReference(in TypeReferenceContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitMemberReference(in MemberReferenceContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitMethodImplementation(in MethodImplementationContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitExportedType(in ExportedTypeContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitManifestResource(in ManifestResourceContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitAssemblyReference(in AssemblyReferenceContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitModuleReference(in ModuleReferenceContext value) { Sum += MetadataTokens.GetRowNumber(value.Handle); }
        public void VisitAssemblyFile(in AssemblyFile value, AssemblyFileHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
        public void VisitTypeSpecification(in TypeSpecification value, TypeSpecificationHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
        public void VisitMethodSpecification(in MethodSpecification value, MethodSpecificationHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
        public void VisitStandaloneSignature(in StandaloneSignature value, StandaloneSignatureHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
        public void VisitConstant(in Constant value, ConstantHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
        public void VisitDeclarativeSecurityAttribute(in DeclarativeSecurityAttribute value, DeclarativeSecurityAttributeHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
        public void VisitCustomDebugInformation(in CustomDebugInformation value, CustomDebugInformationHandle handle) { Sum += MetadataTokens.GetRowNumber(handle); }
    }

    public void Dispose() => _corpus.Dispose();
}

using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using Glean.Contexts;

namespace Glean.Benchmarks;

[MemoryDiagnoser]
[MedianColumn]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[JsonExporterAttribute.Full]
public class FastBenchmarks : IDisposable
{
    private MetadataFixture _fixture = null!;
    private AssemblyContext _assembly;

    [GlobalSetup]
    public void Setup()
    {
        _fixture = new MetadataFixture();
        _assembly = AssemblyContext.Create(_fixture.Reader);
        int typeRows = MetadataFixture.TypeCount + 1;
        int expectedTypes = typeRows * (typeRows + 1) / 2 + MetadataFixture.TypeCount / 2;
        int methodRows = MetadataFixture.TypeCount * MetadataFixture.MethodsPerType;
        int expectedMethods = methodRows * (methodRows + 1) / 2 + methodRows / 2 *
            ((int)(MethodAttributes.Public | MethodAttributes.Static) + (int)(MethodAttributes.Private | MethodAttributes.Static));
        MetadataFixture.Require(SrmTypes() == expectedTypes, "Type traversal fixture checksum");
        MetadataFixture.Require(SrmMethods() == expectedMethods, "Method traversal fixture checksum");
        MetadataFixture.Require(SrmTypes() == GleanTypes(), "Type traversal equivalence");
        MetadataFixture.Require(SrmMethods() == GleanMethods(), "Method traversal equivalence");
        MetadataFixture.Require(SrmFilter() == GleanFilter(), "Filter equivalence");
        MetadataFixture.Require(SrmPropertySignatures() == MetadataFixture.TypeCount, "Property signature fixture");
        MetadataFixture.Require(SrmPropertySignatures() == GleanPropertySignatures(), "Signature inspection equivalence");
        MetadataFixture.Require(GleanFilter() == ExpectedFilterChecksum(), "Filter fixture checksum");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Fast.Types")]
    public int SrmTypes()
    {
        int checksum = 0;
        foreach (var handle in _fixture.Reader.TypeDefinitions)
        {
            var type = _fixture.Reader.GetTypeDefinition(handle);
            checksum += MetadataTokens.GetRowNumber(handle) + (int)type.Attributes;
        }

        return checksum;
    }

    [Benchmark, BenchmarkCategory("Fast.Types")]
    public int GleanTypes()
    {
        int checksum = 0;
        foreach (var type in _assembly.EnumerateTypes())
        {
            checksum += MetadataTokens.GetRowNumber(type.Handle) + (int)type.Attributes;
        }

        return checksum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Fast.Methods")]
    public int SrmMethods()
    {
        int checksum = 0;
        foreach (var typeHandle in _fixture.Reader.TypeDefinitions)
        {
            var type = _fixture.Reader.GetTypeDefinition(typeHandle);
            foreach (var handle in type.GetMethods())
            {
                var method = _fixture.Reader.GetMethodDefinition(handle);
                checksum += MetadataTokens.GetRowNumber(handle) + (int)method.Attributes;
            }
        }

        return checksum;
    }

    [Benchmark, BenchmarkCategory("Fast.Methods")]
    public int GleanMethods()
    {
        int checksum = 0;
        foreach (var type in _assembly.EnumerateTypes())
        {
            foreach (var method in type.EnumerateMethods())
            {
                checksum += MetadataTokens.GetRowNumber(method.Handle) + (int)method.Attributes;
            }
        }

        return checksum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Fast.Filter")]
    public int SrmFilter()
    {
        var reader = _fixture.Reader;
        int checksum = 0;
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if (((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) ||
                !reader.StringComparer.Equals(type.Namespace, MetadataFixture.TargetNamespace))
            {
                continue;
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if (((method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public) &&
                    reader.StringComparer.Equals(method.Name, "Echo"))
                {
                    checksum += MetadataTokens.GetRowNumber(methodHandle);
                }
            }
        }

        return checksum;
    }

    [Benchmark, BenchmarkCategory("Fast.Filter")]
    public int GleanFilter()
    {
        int checksum = 0;
        foreach (var type in _assembly.EnumerateTypes())
        {
            if (!type.IsPublic || !type.NamespaceIs(MetadataFixture.TargetNamespace))
            {
                continue;
            }

            foreach (var method in type.EnumerateMethods())
            {
                if (method.IsPublic && method.NameIs("Echo"))
                {
                    checksum += MetadataTokens.GetRowNumber(method.Handle);
                }
            }
        }

        return checksum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Fast.SignatureInspection")]
    public int SrmPropertySignatures()
    {
        int checksum = 0;
        foreach (var handle in _fixture.Reader.PropertyDefinitions)
        {
            var property = _fixture.Reader.GetPropertyDefinition(handle);
            var blob = _fixture.Reader.GetBlobReader(property.Signature);
            blob.ReadByte();
            checksum += blob.ReadCompressedInteger();
        }

        return checksum;
    }

    [Benchmark, BenchmarkCategory("Fast.SignatureInspection")]
    public int GleanPropertySignatures()
    {
        int checksum = 0;
        foreach (var handle in _fixture.Reader.PropertyDefinitions)
        {
            var property = _fixture.Reader.GetPropertyDefinition(handle);
            if (!SignatureInspector.TryGetPropertyParameterCount(_fixture.Reader, property.Signature, out int count))
            {
                throw new InvalidOperationException("Invalid fixture signature.");
            }

            checksum += count;
        }

        return checksum;
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _fixture?.Dispose();
    }

    private static int ExpectedFilterChecksum()
    {
        int checksum = 0;
        for (int i = 0; i < MetadataFixture.TypeCount; i += 2)
        {
            checksum += i * MetadataFixture.MethodsPerType + 1;
        }

        return checksum;
    }
}

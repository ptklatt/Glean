using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;

namespace Glean.Tests.Resolution;

public sealed class MemberResolutionIndexBucketTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetCandidates_MixedNamesAndOverloads_PreservesIdentityAndOrder(bool cacheSignatures)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("candidates.dll"), metadata.GetOrAddGuid(Guid.Empty), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("candidates"), new Version(1, 0, 0, 0),
            default, default, 0, AssemblyHashAlgorithm.None);
        var type = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Owner"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var instanceSignature = metadata.GetOrAddBlob(new byte[] { 0x20, 0x01, 0x01, 0x08 });
        var genericSignature = metadata.GetOrAddBlob(new byte[] { 0x10, 0x01, 0x02, 0x01, 0x08, 0x0e });
        var varargSignature = metadata.GetOrAddBlob(new byte[] { 0x05, 0x01, 0x01, 0x08 });
        var simpleSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 });
        var fieldSignature = metadata.GetOrAddBlob(new byte[] { 0x06, 0x08 });
        var methods = new Dictionary<string, List<MethodDefinitionHandle>>(StringComparer.Ordinal);
        var fields = new Dictionary<string, List<FieldDefinitionHandle>>(StringComparer.Ordinal);
        var shapes = new Dictionary<MethodDefinitionHandle, MemberResolutionIndex.MethodSignatureShape>();
        var names = new List<string> { "Single", "Many", "many", "Many", "Malformed" };
        names.AddRange(Enumerable.Repeat("Many", 62));
        foreach (string name in names)
        {
            var nameHandle = metadata.GetOrAddString(name);
            bool generic = (name == "Many") && methods.TryGetValue(name, out var previous) && ((previous.Count % 2) == 0);
            var signature = name == "Malformed" ? default : name == "Single" ? instanceSignature :
                name == "many" ? varargSignature : generic ? genericSignature : simpleSignature;
            var attributes = name == "Single" ? MethodAttributes.Public : MethodAttributes.Public | MethodAttributes.Static;
            var method = metadata.AddMethodDefinition(attributes,
                MethodImplAttributes.Runtime, nameHandle, signature,
                0, MetadataTokens.ParameterHandle(1));
            if (generic)
            {
                metadata.AddGenericParameter(method, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
            }

            shapes.Add(method, name == "Single" ? new(0x20, 0, 1) : name == "many" ? new(0x05, 0, 1) :
                generic ? new(0x10, 1, 2) : default);
            var field = metadata.AddFieldDefinition(FieldAttributes.Public, nameHandle, fieldSignature);
            if (!methods.TryGetValue(name, out var methodList))
            {
                methodList = new List<MethodDefinitionHandle>();
                methods.Add(name, methodList);
                fields.Add(name, new List<FieldDefinitionHandle>());
            }

            methodList.Add(method);
            fields[name].Add(field);
        }

        var blob = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(blob, 0, 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(blob.ToArray()));
        var reader = provider.GetMetadataReader();
        var index = new MemberResolutionIndex(cacheSignatures);
        foreach (string name in new[] { "Single", "Many", "many", "Malformed", "MANY", "Missing" })
        {
            var expectedMethods = methods.TryGetValue(name, out var methodList)
                ? methodList.ToArray() : Array.Empty<MethodDefinitionHandle>();
            var expectedFields = fields.TryGetValue(name, out var fieldList)
                ? fieldList.ToArray() : Array.Empty<FieldDefinitionHandle>();
            var candidates = index.GetMethodCandidatesWithShapes(reader, type, name);

            Assert.Equal(expectedMethods, candidates.Select(candidate => candidate.Handle));
            Assert.Equal(expectedMethods, index.GetMethodCandidates(reader, type, name));
            Assert.Equal(expectedFields, index.GetFieldCandidates(reader, type, name));
            foreach (var candidate in candidates)
            {
                Assert.Equal(reader.GetMethodDefinition(candidate.Handle).Signature, candidate.Signature);
                Assert.Equal(name != "Malformed", candidate.HasShape);
                Assert.Equal(shapes[candidate.Handle], candidate.Shape);
            }
        }
    }
}

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Xunit;

using Glean.Resolution;

namespace Glean.Tests.Resolution;

public sealed class IndexCandidateArrayTests
{
    [Theory]
    [InlineData("shapes")]
    [InlineData("handles")]
    [InlineData("fields")]
    public void GetCandidates_ReusesReturnedArraysUntilClear(string firstApi)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("candidates"), default, default, default);
        var owner = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Owner"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var methodSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 });
        var fieldSignature = metadata.GetOrAddBlob(new byte[] { 0x06, 0x08 });
        foreach (string name in new[] { "Single", "Many", "Many", "Many" })
        {
            metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.Runtime,
                metadata.GetOrAddString(name), methodSignature, 0, MetadataTokens.ParameterHandle(1));
            metadata.AddFieldDefinition(FieldAttributes.Public, metadata.GetOrAddString(name), fieldSignature);
        }
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(image.ToArray()));
        var reader = provider.GetMetadataReader();
        var index = new MemberResolutionIndex();
        switch (firstApi)
        {
            case "shapes":
                index.GetMethodCandidatesWithShapes(reader, owner, "Many");
                break;
            case "handles":
                index.GetMethodCandidates(reader, owner, "Many");
                break;
            case "fields":
                index.GetFieldCandidates(reader, owner, "Many");
                break;
        }

        foreach (string name in new[] { "Single", "Many" })
        {
            var expectedMethods = reader.GetTypeDefinition(owner).GetMethods()
                .Where(handle => reader.StringComparer.Equals(reader.GetMethodDefinition(handle).Name, name)).ToArray();
            var expectedFields = reader.GetTypeDefinition(owner).GetFields()
                .Where(handle => reader.StringComparer.Equals(reader.GetFieldDefinition(handle).Name, name)).ToArray();
            var shaped = index.GetMethodCandidatesWithShapes(reader, owner, name);
            var methods = index.GetMethodCandidates(reader, owner, name);
            var fields = index.GetFieldCandidates(reader, owner, name);
            Assert.Equal(expectedMethods, shaped.Select(candidate => candidate.Handle));
            Assert.Equal(expectedMethods, methods);
            Assert.Equal(expectedFields, fields);
            Assert.Same(shaped, index.GetMethodCandidatesWithShapes(reader, owner, name));
            Assert.Same(methods, index.GetMethodCandidates(reader, owner, name));
            Assert.Same(fields, index.GetFieldCandidates(reader, owner, name));

            shaped[0] = default;
            methods[0] = default;
            fields[0] = default;
            Assert.True(index.GetMethodCandidatesWithShapes(reader, owner, name)[0].Handle.IsNil);
            Assert.True(index.GetMethodCandidates(reader, owner, name)[0].IsNil);
            Assert.True(index.GetFieldCandidates(reader, owner, name)[0].IsNil);

            index.Clear();
            var rebuiltShapes = index.GetMethodCandidatesWithShapes(reader, owner, name);
            var rebuiltMethods = index.GetMethodCandidates(reader, owner, name);
            var rebuiltFields = index.GetFieldCandidates(reader, owner, name);
            Assert.NotSame(shaped, rebuiltShapes);
            Assert.NotSame(methods, rebuiltMethods);
            Assert.NotSame(fields, rebuiltFields);
            Assert.Equal(expectedMethods, rebuiltShapes.Select(candidate => candidate.Handle));
            Assert.Equal(expectedMethods, rebuiltMethods);
            Assert.Equal(expectedFields, rebuiltFields);
            Assert.True(shaped[0].Handle.IsNil);
            Assert.True(methods[0].IsNil);
            Assert.True(fields[0].IsNil);
        }
    }
}

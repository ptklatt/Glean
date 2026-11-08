using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;

using Xunit;

using Glean.Resolution;

namespace Glean.Tests.Resolution;

public sealed class MemberNameResolutionTests
{
    public static IEnumerable<object[]> Names()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            foreach (bool field in new[] { false, true })
            {
                yield return new object[] { mode, field, "Member", "Member", true };
                yield return new object[] { mode, field, "Member", "member", false };
                yield return new object[] { mode, field, "Member", "Other!", false };
                yield return new object[] { mode, field, "Mémber", "Mémber", true };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TryResolveMember_DistinctStringOffsets_UsesOrdinalNameValues(
        int mode, bool field, string definitionName, string referenceName, bool expected)
    {
        using var fixture = new NameFixture(field, definitionName, referenceName);
        using var set = new AssemblySet();
        set.Add(fixture.Reader);
        var member = fixture.Reader.GetMemberReference(fixture.Reference);
        var name = field
            ? fixture.Reader.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(1)).Name
            : fixture.Reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(1)).Name;
        Assert.NotEqual(name, member.Name);
        Assert.Equal(definitionName, fixture.Reader.GetString(name));
        Assert.Equal(referenceName, fixture.Reader.GetString(member.Name));

        var index = mode == 0 ? null : new MemberResolutionIndex(cacheSemanticSignatures: mode == 2);
        for (int pass = 0; pass < 2; pass++)
        {
            MetadataReader targetReader;
            EntityHandle target;
            ResolutionFailureReason reason;
            bool resolved = index == null
                ? set.TryResolveMember(fixture.Reader, fixture.Reference, out targetReader, out target, out reason)
                : set.TryResolveMember(fixture.Reader, fixture.Reference, index, out targetReader, out target, out reason);

            Assert.Equal(expected, resolved);
            if (expected)
            {
                Assert.Same(fixture.Reader, targetReader);
                EntityHandle definition = field ? MetadataTokens.FieldDefinitionHandle(1) : MetadataTokens.MethodDefinitionHandle(1);
                Assert.Equal(definition, target);
                Assert.Equal(ResolutionFailureReason.None, reason);
            }
            else
            {
                Assert.Null(targetReader);
                Assert.True(target.IsNil);
                Assert.Equal(ResolutionFailureReason.MemberNotFound, reason);
            }
        }
    }

    private sealed class NameFixture : IDisposable
    {
        private readonly MetadataReaderProvider _provider;

        public NameFixture(bool field, string definitionName, string referenceName)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(0, metadata.GetOrAddString("Names"), default, default, default);
            metadata.AddAssembly(metadata.GetOrAddString("Names"), new Version(1, 0, 0, 0),
                default, default, 0, AssemblyHashAlgorithm.None);
            var owner = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Owner"),
                default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            var signature = metadata.GetOrAddBlob(field ? new byte[] { 6, 8 } : new byte[] { 0, 0, 1 });
            var name = metadata.GetOrAddString(definitionName);
            if (field)
            {
                metadata.AddFieldDefinition(FieldAttributes.Public, name, signature);
            }
            else
            {
                metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.Runtime,
                    name, signature, 0, MetadataTokens.ParameterHandle(1));
            }
            byte[] replacement = Encoding.UTF8.GetBytes(referenceName);
            Reference = metadata.AddMemberReference(owner, metadata.GetOrAddString(new string('Z', replacement.Length)), signature);
            var blob = new BlobBuilder();
            new MetadataRootBuilder(metadata).Serialize(blob, 0, 0);
            byte[] bytes = blob.ToArray();
            using (var original = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes)))
            {
                var reader = original.GetMetadataReader();
                var referenceNameHandle = reader.GetMemberReference(Reference).Name;
                int offset = reader.GetHeapMetadataOffset(HeapIndex.String) + MetadataTokens.GetHeapOffset(referenceNameHandle);
                replacement.CopyTo(bytes, offset);
            }
            _provider = MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(bytes));
            Reader = _provider.GetMetadataReader();
        }

        public MetadataReader Reader { get; }
        public MemberReferenceHandle Reference { get; }

        public void Dispose() => _provider.Dispose();
    }
}

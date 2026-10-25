using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Resolution;
using Glean.Tests.Utility;

namespace Glean.Tests.Resolution;

public sealed class AssemblySetSignatureTests
{
    public static IEnumerable<object[]> DistinctSignatures()
    {
        var types = new (string Name, byte[] Reference, byte[] Wrong, byte[] Definition)[]
        {
            ("primitive", [0x08], [0x0e], [0x08]),
            ("named type", [0x12, 0x09], [0x0e], [0x12, 0x0c]),
            ("vector", [0x1d, 0x08], [0x14, 0x08, 0x01, 0x00, 0x00], [0x1d, 0x08]),
            ("array size", [0x14, 0x08, 0x02, 0x01, 0x03, 0x01, 0x00],
                [0x14, 0x08, 0x02, 0x01, 0x04, 0x01, 0x00],
                [0x14, 0x08, 0x02, 0x01, 0x03, 0x01, 0x00]),
            ("pointer", [0x0f, 0x08], [0x10, 0x08], [0x0f, 0x08]),
            ("generic instance", [0x15, 0x12, 0x09, 0x01, 0x08],
                [0x15, 0x12, 0x0c, 0x01, 0x0e], [0x15, 0x12, 0x0c, 0x01, 0x08]),
            ("modifier kind", [0x1f, 0x09, 0x08], [0x20, 0x0c, 0x08], [0x1f, 0x0c, 0x08]),
            ("modifier order", [0x1f, 0x09, 0x1f, 0x0d, 0x08],
                [0x1f, 0x09, 0x1f, 0x0c, 0x08], [0x1f, 0x0c, 0x1f, 0x09, 0x08]),
            ("function pointer convention", [0x1b, 0x01, 0x00, 0x01],
                [0x1b, 0x02, 0x00, 0x01], [0x1b, 0x01, 0x00, 0x01]),
            ("function pointer varargs", [0x1b, 0x05, 0x02, 0x01, 0x08, 0x41, 0x0e],
                [0x1b, 0x05, 0x02, 0x01, 0x08, 0x0e],
                [0x1b, 0x05, 0x02, 0x01, 0x08, 0x41, 0x0e])
        };

        for (int mode = 0; mode < 3; mode++)
        {
            foreach (bool field in new[] { false, true })
            {
                foreach (var type in types)
                {
                    yield return [mode, field, type.Name, Wrap(type.Reference, field),
                        Wrap(type.Wrong, field), Wrap(type.Definition, field)];
                }
            }

            yield return [mode, false, "return type", new byte[] { 0x00, 0x00, 0x08 },
                new byte[] { 0x00, 0x00, 0x0e }, new byte[] { 0x00, 0x00, 0x08 }];
            yield return [mode, false, "generic arity", new byte[] { 0x10, 0x02, 0x00, 0x01 },
                new byte[] { 0x10, 0x01, 0x00, 0x01 }, new byte[] { 0x10, 0x02, 0x00, 0x01 }];
            yield return [mode, false, "instance header", new byte[] { 0x20, 0x00, 0x01 },
                new byte[] { 0x00, 0x00, 0x01 }, new byte[] { 0x20, 0x00, 0x01 }];
        }
    }

    [Theory]
    [MemberData(nameof(DistinctSignatures))]
    public void TryResolveMember_SameNameDifferentSignatures_SelectsOnlyMatchingDefinition(
        int mode, bool field, string scenario, byte[] reference, byte[] wrong, byte[] expected)
    {
        using var fixture = new SignatureFixture(field, reference, [wrong, expected]);
        using var set = new AssemblySet();
        set.Add(fixture.Definition.Reader);

        Assert.True(TryResolve(set, fixture.Reference.Reader, fixture.MemberReference, mode,
            out var targetReader, out var targetHandle, out var reason), scenario);
        Assert.Equal(ResolutionFailureReason.None, reason);
        Assert.Same(fixture.Definition.Reader, targetReader);
        Assert.Equal(field ? HandleKind.FieldDefinition : HandleKind.MethodDefinition, targetHandle.Kind);
        Assert.Equal(2, MetadataTokens.GetRowNumber(targetHandle));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TryResolveMember_FieldNameMatchesButTypeDiffers_ReturnsMemberNotFound(int mode)
    {
        using var fixture = new SignatureFixture(true, [0x06, 0x08], [[0x06, 0x0e]]);
        using var set = new AssemblySet();
        set.Add(fixture.Definition.Reader);

        Assert.False(TryResolve(set, fixture.Reference.Reader, fixture.MemberReference, mode,
            out var targetReader, out var targetHandle, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberNotFound, reason);
        Assert.Null(targetReader);
        Assert.True(targetHandle.IsNil);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void TryResolveMember_DuplicateMatchingDefinitions_ReturnsMemberAmbiguous(int mode, bool field)
    {
        var signature = Wrap([0x08], field);
        using var fixture = new SignatureFixture(field, signature, [signature, signature]);
        using var set = new AssemblySet();
        set.Add(fixture.Definition.Reader);

        Assert.False(TryResolve(set, fixture.Reference.Reader, fixture.MemberReference, mode,
            out var targetReader, out var targetHandle, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberAmbiguous, reason);
        Assert.Null(targetReader);
        Assert.True(targetHandle.IsNil);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void TryResolveMember_ByteAndSemanticMatchesAtSameLevel_ReturnsMemberAmbiguous(int mode, bool field)
    {
        var reference = Wrap([0x12, 0x05], field);
        var equivalent = Wrap([0x12, 0x0c], field);
        using var fixture = new SignatureFixture(field, reference, [reference, equivalent], sameReader: true);
        using var set = new AssemblySet();
        set.Add(fixture.Definition.Reader);

        Assert.False(TryResolve(set, fixture.Definition.Reader, fixture.MemberReference, mode,
            out var targetReader, out var targetHandle, out var reason));
        Assert.Equal(ResolutionFailureReason.MemberAmbiguous, reason);
        Assert.Null(targetReader);
        Assert.True(targetHandle.IsNil);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TryResolveMember_WarmedIndexedSignatureResolution_DoesNotAllocate(bool cacheSignatures, bool field)
    {
        using var fixture = new SignatureFixture(field, Wrap([0x12, 0x09], field),
            [Wrap([0x0e], field), Wrap([0x12, 0x0c], field)]);
        using var set = new AssemblySet();
        set.Add(fixture.Definition.Reader);
        var index = new MemberResolutionIndex(cacheSignatures);
        bool allResolved = true;

        for (int i = 0; i < 32; i++)
        {
            allResolved &= set.TryResolveMember(fixture.Reference.Reader, fixture.MemberReference, index, out _, out _);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            allResolved &= set.TryResolveMember(fixture.Reference.Reader, fixture.MemberReference, index, out _, out _);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allResolved);
        Assert.Equal(0L, allocated);

        index.Clear();
        set.ClearCaches();
        Assert.True(set.TryResolveMember(fixture.Reference.Reader, fixture.MemberReference, index,
            out var targetReader, out var targetHandle));
        Assert.Same(fixture.Definition.Reader, targetReader);
        Assert.Equal(2, MetadataTokens.GetRowNumber(targetHandle));
    }

    [Fact]
    public void TryResolveMember_CachedFieldSignaturesFromDifferentReaders_DoNotCollide()
    {
        using var first = new SignatureFixture(true, [0x06, 0x08], [[0x06, 0x0e], [0x06, 0x08]]);
        using var second = new SignatureFixture(true, [0x06, 0x0e], [[0x06, 0x08], [0x06, 0x0e]]);
        using var set = new AssemblySet();
        var index = new MemberResolutionIndex(cacheSemanticSignatures: true);

        set.Add(first.Definition.Reader);
        Assert.True(set.TryResolveMember(first.Reference.Reader, first.MemberReference, index,
            out var firstReader, out var firstHandle));
        Assert.Same(first.Definition.Reader, firstReader);
        Assert.Equal(2, MetadataTokens.GetRowNumber(firstHandle));

        set.Add(second.Definition.Reader);
        Assert.True(set.TryResolveMember(second.Reference.Reader, second.MemberReference, index,
            out var secondReader, out var secondHandle));
        Assert.Same(second.Definition.Reader, secondReader);
        Assert.Equal(2, MetadataTokens.GetRowNumber(secondHandle));
    }

    private static byte[] Wrap(byte[] type, bool field)
    {
        return field ? [0x06, .. type] : [0x00, 0x01, 0x01, .. type];
    }

    private static bool TryResolve(AssemblySet set, MetadataReader reader, MemberReferenceHandle reference,
        int mode, out MetadataReader targetReader, out EntityHandle targetHandle, out ResolutionFailureReason reason)
    {
        return mode == 0
            ? set.TryResolveMember(reader, reference, out targetReader, out targetHandle, out reason)
            : set.TryResolveMember(reader, reference, new MemberResolutionIndex(mode == 2),
                out targetReader, out targetHandle, out reason);
    }

    private sealed class SignatureFixture : IDisposable
    {
        public SignatureFixture(bool field, byte[] referenceSignature, byte[][] definitions, bool sameReader = false)
        {
            var metadata = CreateAssembly("Library");
            var self = AddAssemblyReference(metadata, "Library");
            metadata.AddTypeReference(self, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("Marker`1"));
            metadata.AddTypeReference(self, metadata.GetOrAddString("Fixture"), metadata.GetOrAddString("OtherMarker"));
            var owner = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"),
                metadata.GetOrAddString("Owner"), default,
                MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));

            foreach (var definition in definitions)
            {
                var blob = metadata.GetOrAddBlob(definition);
                var name = metadata.GetOrAddString("Member");
                if (field)
                {
                    metadata.AddFieldDefinition(FieldAttributes.Public, name, blob);
                }
                else
                {
                    metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static,
                        MethodImplAttributes.IL, name, blob, -1, MetadataTokens.ParameterHandle(1));
                }
            }

            var marker = metadata.AddTypeDefinition(TypeAttributes.Public, metadata.GetOrAddString("Fixture"),
                metadata.GetOrAddString("Marker`1"), default,
                MetadataTokens.FieldDefinitionHandle(field ? definitions.Length + 1 : 1),
                MetadataTokens.MethodDefinitionHandle(field ? 1 : definitions.Length + 1));
            metadata.AddGenericParameter(marker, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);

            if (sameReader)
            {
                MemberReference = metadata.AddMemberReference(owner, metadata.GetOrAddString("Member"),
                    metadata.GetOrAddBlob(referenceSignature));
                Definition = Open(metadata);
                Reference = Definition;
            }
            else
            {
                Definition = Open(metadata);
                var referenceMetadata = CreateAssembly("Consumer");
                var library = AddAssemblyReference(referenceMetadata, "Library");
                var ownerReference = referenceMetadata.AddTypeReference(library,
                    referenceMetadata.GetOrAddString("Fixture"), referenceMetadata.GetOrAddString("Owner"));
                referenceMetadata.AddTypeReference(library, referenceMetadata.GetOrAddString("Fixture"),
                    referenceMetadata.GetOrAddString("Marker`1"));
                referenceMetadata.AddTypeReference(library, referenceMetadata.GetOrAddString("Fixture"),
                    referenceMetadata.GetOrAddString("OtherMarker"));
                MemberReference = referenceMetadata.AddMemberReference(ownerReference,
                    referenceMetadata.GetOrAddString("Member"), referenceMetadata.GetOrAddBlob(referenceSignature));
                Reference = Open(referenceMetadata);
            }
        }

        public MetadataScope Definition { get; }
        public MetadataScope Reference { get; }
        public MemberReferenceHandle MemberReference { get; }

        public void Dispose()
        {
            if (!ReferenceEquals(Reference, Definition))
            {
                Reference.Dispose();
            }

            Definition.Dispose();
        }

        private static MetadataBuilder CreateAssembly(string name)
        {
            var metadata = new MetadataBuilder();
            metadata.AddModule(0, metadata.GetOrAddString(name + ".dll"),
                metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
            metadata.AddAssembly(metadata.GetOrAddString(name), new Version(1, 0, 0, 0),
                default, default, 0, AssemblyHashAlgorithm.None);
            metadata.AddTypeDefinition(TypeAttributes.NotPublic, default, metadata.GetOrAddString("<Module>"),
                default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
            return metadata;
        }

        private static AssemblyReferenceHandle AddAssemblyReference(MetadataBuilder metadata, string name)
        {
            return metadata.AddAssemblyReference(metadata.GetOrAddString(name), new Version(1, 0, 0, 0),
                default, default, 0, default);
        }

        private static MetadataScope Open(MetadataBuilder metadata)
        {
            var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata),
                new BlobBuilder(), flags: CorFlags.ILOnly);
            var image = new BlobBuilder();
            pe.Serialize(image);
            return new MetadataScope(new MemoryStream(image.ToArray(), writable: false));
        }
    }
}

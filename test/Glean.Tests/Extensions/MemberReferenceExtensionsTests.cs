using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class MemberReferenceExtensionsTests
{
    private const string MemberReferenceSource = """
        using System;

        namespace Glean.Tests.Extensions;

        internal static class MemberReferenceFixture
        {
            internal static string ReadField()
            {
                return string.Empty;
            }

            internal static void CallMethod()
            {
                Console.WriteLine(string.Empty);
            }
        }
        """;

    // == Kind and identity helpers ==========================================

    [Fact]
    public void KindAndIdentityHelpers_FrameworkMemberReferences_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(MemberReferenceSource);

        var fieldReference = GetTypeMemberReference(metadata, "System", "String", "Empty");
        var methodReference = GetTypeMemberReference(metadata, "System", "Console", "WriteLine");

        Assert.Equal(SignatureKind.Field, fieldReference.GetSignatureKind(metadata.Reader));
        Assert.True(fieldReference.IsFieldReference(metadata.Reader));
        Assert.False(fieldReference.IsMethodReference(metadata.Reader));
        Assert.True(fieldReference.NameIs(metadata.Reader, "Empty"));
        Assert.False(fieldReference.NameIs(metadata.Reader, "WriteLine"));

        Assert.Equal(SignatureKind.Method, methodReference.GetSignatureKind(metadata.Reader));
        Assert.True(methodReference.IsMethodReference(metadata.Reader));
        Assert.False(methodReference.IsFieldReference(metadata.Reader));
        Assert.True(methodReference.NameIs(metadata.Reader, "WriteLine"));
        Assert.False(methodReference.NameIs(metadata.Reader, "Empty"));
    }

    // == Decode ============================================================== 

    [Fact]
    public void DecodeFieldSignature_StringEmptyReference_ReturnsStringSignature()
    {
        using var metadata = TestUtility.BuildMetadata(MemberReferenceSource);
        var fieldReference = GetTypeMemberReference(metadata, "System", "String", "Empty");

        TypeSignature signature = fieldReference.DecodeFieldSignature(
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty);

        var primitive = Assert.IsType<PrimitiveTypeSignature>(signature);
        Assert.Equal(PrimitiveTypeCode.String, primitive.TypeCode);
    }

    [Fact]
    public void DecodeMethodSignature_ConsoleWriteLineReference_ReturnsExpectedSignature()
    {
        using var metadata = TestUtility.BuildMetadata(MemberReferenceSource);
        var methodReference = GetTypeMemberReference(metadata, "System", "Console", "WriteLine");

        MethodSignature<TypeSignature> signature = methodReference.DecodeMethodSignature(
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty);

        Assert.Equal(0, signature.GenericParameterCount);

        var returnType = Assert.IsType<PrimitiveTypeSignature>(signature.ReturnType);
        Assert.Equal(PrimitiveTypeCode.Void, returnType.TypeCode);

        var parameterType = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(signature.ParameterTypes));
        Assert.Equal(PrimitiveTypeCode.String, parameterType.TypeCode);
    }

    [Fact]
    public void DecodeFieldSignature_MethodReference_ThrowsBadImageFormatException()
    {
        using var metadata = TestUtility.BuildMetadata(MemberReferenceSource);
        var methodReference = GetTypeMemberReference(metadata, "System", "Console", "WriteLine");

        Assert.Throws<BadImageFormatException>(() => methodReference.DecodeFieldSignature(
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty));
    }

    [Fact]
    public void DecodeMethodSignature_FieldReference_ThrowsBadImageFormatException()
    {
        using var metadata = TestUtility.BuildMetadata(MemberReferenceSource);
        var fieldReference = GetTypeMemberReference(metadata, "System", "String", "Empty");

        Assert.Throws<BadImageFormatException>(() => fieldReference.DecodeMethodSignature(
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty));
    }

    private static MemberReference GetTypeMemberReference(
        MetadataScope metadata,
        string ns,
        string typeName,
        string memberName)
    {
        foreach (var handle in metadata.Reader.MemberReferences)
        {
            var memberReference = metadata.Reader.GetMemberReference(handle);
            if (!memberReference.NameIs(metadata.Reader, memberName))
            {
                continue;
            }

            if (memberReference.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var parent = metadata.Reader.GetTypeReference((TypeReferenceHandle)memberReference.Parent);
            if (parent.NamespaceIs(metadata.Reader, ns) && parent.NameIs(metadata.Reader, typeName))
            {
                return memberReference;
            }
        }

        throw new InvalidOperationException($"Could not locate member reference {ns}.{typeName}.{memberName}.");
    }
}

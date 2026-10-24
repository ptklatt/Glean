using System.Reflection.Metadata;

using Xunit;

using Glean.Signatures;

namespace Glean.Tests.Signatures;

public sealed class TypeSignatureEqualityTests
{
    [Fact]
    public void EqualityOperators_TwoNullOperands_AreEqual()
    {
        TypeSignature? left = null;
        TypeSignature? right = null;

        Assert.True(left == right);
        Assert.False(left != right);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualityOperators_OneNullOperand_AreNotEqual(bool nullOnLeft)
    {
        var signature = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        TypeSignature? left = nullOnLeft ? null : signature;
        TypeSignature? right = nullOnLeft ? signature : null;

        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void EqualityOperators_SameInstance_AreEqual()
    {
        TypeSignature left = new SZArraySignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32));
        TypeSignature right = left;

        Assert.True(left == right);
        Assert.False(left != right);
    }

    [Fact]
    public void EqualityOperators_DistinctEquivalentSignatures_AreEqual()
    {
        TypeSignature left = new SZArraySignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32));
        TypeSignature right = new SZArraySignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32));

        Assert.True(left == right);
        Assert.False(left != right);
    }

    [Fact]
    public void EqualityOperators_DifferentElementTypes_AreNotEqual()
    {
        TypeSignature left = new SZArraySignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32));
        TypeSignature right = new SZArraySignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.String));

        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void EqualityOperators_DifferentSignatureKinds_AreNotEqual()
    {
        TypeSignature left = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        TypeSignature right = new SZArraySignature(left);

        Assert.False(left == right);
        Assert.True(left != right);
    }
}

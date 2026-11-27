using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using CustomIrSample;
using Xunit;

using Glean.Contexts;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Contexts;

public sealed class MethodSpecificationContextTests
{
    private const string Source = """
        using System;
        namespace Fixtures;
        public static class Methods
        {
            public static void Local<U>() { }
            public static void Calls() { Local<string>(); _ = Array.Empty<int>(); }
        }
        public class Pair<T, V> { }
        public class Host<T>
        {
            public static void Calls<V>() { Methods.Local<T[]>(); Methods.Local<Pair<T, V>>(); }
        }
        """;

    [Fact]
    public void EnumerateMethodSpecifications_PreservesTableOrderTargetsBlobsAndReader()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var reader = metadata.Reader;
        var module = ModuleContext.Create(reader);
        Assert.Equal(4, module.MethodSpecificationCount);
        Assert.Equal(reader.GetTableRowCount(TableIndex.MethodSpec), module.MethodSpecificationCount);
        int row = 0;
        bool local = false;
        bool reference = false;
        foreach (var specification in module.EnumerateMethodSpecifications())
        {
            var handle = MetadataTokens.MethodSpecificationHandle(++row);
            var raw = reader.GetMethodSpecification(handle);
            Assert.True(specification.IsValid);
            Assert.Same(reader, specification.Reader);
            Assert.Equal(handle, specification.Handle);
            Assert.Equal(raw.Method, specification.Method);
            Assert.Equal(raw.Signature, specification.SignatureHandle);
            Assert.Equal(raw.Method, specification.Specification.Method);
            local |= specification.Method.Kind == HandleKind.MethodDefinition;
            reference |= specification.Method.Kind == HandleKind.MemberReference;
        }
        Assert.Equal(module.MethodSpecificationCount, row);
        Assert.True(local);
        Assert.True(reference);
    }

    [Fact]
    public void EnumerateMethodSpecifications_EmptyTable_StaysEmpty()
    {
        using var metadata = TestUtility.BuildMetadata("public class Empty { }");
        var module = ModuleContext.Create(metadata.Reader);
        Assert.Equal(0, module.MethodSpecificationCount);
        var enumerator = module.EnumerateMethodSpecifications();
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void EnumerateMethodSpecifications_CopiedEnumerator_KeepsIndependentPositionAndStopsAtLastRow()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var module = ModuleContext.Create(metadata.Reader);
        var enumerator = module.EnumerateMethodSpecifications();
        Assert.True(enumerator.MoveNext());
        var copy = enumerator.GetEnumerator();
        Assert.Equal(enumerator.Current, copy.Current);
        Assert.True(copy.MoveNext());
        Assert.NotEqual(enumerator.Current, copy.Current);
        int count = 1;
        var lastHandle = copy.Current.Handle;
        while (copy.MoveNext())
        {
            count++;
            lastHandle = copy.Current.Handle;
        }
        Assert.Equal(module.MethodSpecificationCount - 1, count);
        Assert.False(copy.MoveNext());
        Assert.Equal(module.MethodSpecificationCount, MetadataTokens.GetRowNumber(lastHandle));
        Assert.Equal(1, MetadataTokens.GetRowNumber(enumerator.Current.Handle));
    }

    [Fact]
    public void SpecificationIdentity_DifferentReadersOrRows_RemainsDistinct()
    {
        using var first = TestUtility.BuildMetadata(Source);
        using var second = TestUtility.BuildMetadata(Source);
        var handle = MetadataTokens.MethodSpecificationHandle(1);
        var left = MethodSpecificationContext.Create(first.Reader, handle);
        var same = MethodSpecificationContext.Create(first.Reader, handle);
        Assert.True(left == same);
        Assert.True(left.Equals((object)same));
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.True(left != MethodSpecificationContext.Create(second.Reader, handle));
        Assert.True(left != MethodSpecificationContext.Create(first.Reader, MetadataTokens.MethodSpecificationHandle(2)));
        Assert.False(default(MethodSpecificationContext).IsValid);
        Assert.Equal("reader", Assert.Throws<ArgumentNullException>(() => MethodSpecificationContext.Create(null!, handle)).ParamName);
        Assert.Equal("handle", Assert.Throws<ArgumentException>(() => MethodSpecificationContext.Create(first.Reader, default)).ParamName);
    }

    [Fact]
    public void DecodeSignature_DefaultModel_PreservesConcreteAndOpenTypeArguments()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        bool text = false;
        bool integer = false;
        bool array = false;
        bool pair = false;
        foreach (var specification in ModuleContext.Create(metadata.Reader).EnumerateMethodSpecifications())
        {
            var argument = Assert.Single(specification.DecodeSignature());
            switch (argument)
            {
                case PrimitiveTypeSignature primitive:
                    text |= primitive.TypeCode == PrimitiveTypeCode.String;
                    integer |= primitive.TypeCode == PrimitiveTypeCode.Int32;
                    break;
                case SZArraySignature vector:
                    Assert.Equal(0, Assert.IsType<GenericTypeParameterSignature>(vector.ElementType).Index);
                    array = true;
                    break;
                case GenericInstanceSignature generic:
                    Assert.True(generic.GenericType.Is("Fixtures", "Pair`2"));
                    Assert.Equal(2, generic.Arguments.Length);
                    Assert.Equal(0, Assert.IsType<GenericTypeParameterSignature>(generic.Arguments[0]).Index);
                    Assert.Equal(0, Assert.IsType<GenericMethodParameterSignature>(generic.Arguments[1]).Index);
                    pair = true;
                    break;
                default:
                    throw new InvalidOperationException("Unexpected instantiation argument.");
            }
        }
        Assert.True(text && integer && array && pair);
    }

    [Fact]
    public void DecodeSignature_CustomModel_SubstitutesEnclosingTypeAndMethodArguments()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var context = new IrGenericContext(
            ImmutableArray.Create<IrType>(new IrType.Primitive(PrimitiveTypeCode.Boolean)),
            ImmutableArray.Create<IrType>(new IrType.Primitive(PrimitiveTypeCode.Double)));
        var provider = new IrTypeProvider();
        bool array = false;
        bool pair = false;
        foreach (var specification in ModuleContext.Create(metadata.Reader).EnumerateMethodSpecifications())
        {
            var arguments = specification.DecodeSignature(provider, context);
            var argument = Assert.Single(arguments);
            Assert.Equal(specification.Specification.DecodeSignature(provider, context).Length, arguments.Length);
            if (argument is IrType.Vector vector)
            {
                Assert.Equal(context.TypeArguments[0], vector.Element);
                array = true;
            }
            else if (argument is IrType.GenericInstance generic)
            {
                Assert.Equal("Fixtures.Pair`2", Assert.IsType<IrType.Named>(generic.Definition).Identity.FullName);
                Assert.Equal(2, generic.Arguments.Length);
                Assert.Equal(context.TypeArguments[0], generic.Arguments[0]);
                Assert.Equal(context.MethodArguments[0], generic.Arguments[1]);
                pair = true;
            }
        }
        Assert.True(array && pair);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x06, 0x08 })]
    [InlineData(new byte[] { 0x0a, 1 })]
    [InlineData(new byte[] { 0x0a, 0xff })]
    public void DecodeSignature_MalformedInstantiation_PropagatesBadImageFormat(byte[] signature)
    {
        using var owner = CreateSyntheticMetadata(signature, out var handle, out _);
        var specification = MethodSpecificationContext.Create(owner.GetMetadataReader(), handle);
        Assert.Throws<BadImageFormatException>(() => specification.DecodeSignature());
        Assert.Throws<BadImageFormatException>(() => specification.DecodeSignature(new IrTypeProvider(), default(IrGenericContext)));
    }

    [Fact]
    public void EnumerateCustomAttributes_ReadsAttributesOnInstantiationRows()
    {
        using var owner = CreateSyntheticMetadata(new byte[] { 0x0a, 1, 0x08 }, out var handle, out var attribute);
        var reader = owner.GetMetadataReader();
        var specification = MethodSpecificationContext.Create(reader, handle);
        var attributes = specification.EnumerateCustomAttributes();
        Assert.True(attributes.MoveNext());
        Assert.Same(reader, attributes.Current.Reader);
        Assert.Equal(attribute, attributes.Current.Handle);
        Assert.False(attributes.MoveNext());
        Assert.Equal(PrimitiveTypeCode.Int32, Assert.IsType<PrimitiveTypeSignature>(Assert.Single(specification.DecodeSignature())).TypeCode);
    }

    [Fact]
    public void DecodeSignature_NullProvider_ReportsArgumentName()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var specification = MethodSpecificationContext.Create(metadata.Reader, MetadataTokens.MethodSpecificationHandle(1));
        Assert.Equal("provider", Assert.Throws<ArgumentNullException>(() => specification.DecodeSignature<IrType, IrGenericContext>(null!, default)).ParamName);
    }

    [Fact]
    public void EnumerateMethodSpecifications_RepeatedRawScans_AllocateNothing()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var module = ModuleContext.Create(metadata.Reader);
        int expected = Scan(module);
        for (int i = 0; i < 10_000; i++)
        {
            Scan(module);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 20_000; i++)
        {
            sum += Scan(module);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected * 20_000, sum);
        Assert.Equal(0, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Scan(ModuleContext module)
    {
        int sum = module.MethodSpecificationCount;
        foreach (var specification in module.EnumerateMethodSpecifications())
        {
            sum += MetadataTokens.GetRowNumber(specification.Handle);
            sum += MetadataTokens.GetRowNumber(specification.Method);
            sum += MetadataTokens.GetHeapOffset(specification.SignatureHandle);
        }
        return sum;
    }

    private static MetadataReaderProvider CreateSyntheticMetadata(byte[] signature, out MethodSpecificationHandle handle,
        out CustomAttributeHandle attribute)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Specifications"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        var type = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Target"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var method = metadata.AddMethodDefinition(MethodAttributes.Public | MethodAttributes.Static, MethodImplAttributes.IL,
            metadata.GetOrAddString("Generic"), metadata.GetOrAddBlob(new byte[] { 0x10, 1, 0, 1 }), 0, MetadataTokens.ParameterHandle(1));
        metadata.AddGenericParameter(method, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
        handle = metadata.AddMethodSpecification(method, metadata.GetOrAddBlob(signature));
        var constructor = metadata.AddMemberReference(type, metadata.GetOrAddString(".ctor"), metadata.GetOrAddBlob(new byte[] { 0x20, 0, 1 }));
        attribute = metadata.AddCustomAttribute(handle, constructor, metadata.GetOrAddBlob(new byte[] { 1, 0, 0, 0 }));
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }
}

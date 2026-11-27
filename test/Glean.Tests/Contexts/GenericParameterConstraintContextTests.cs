using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using CustomIrSample;
using Xunit;

using Glean.Contexts;
using Glean.Extensions;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Contexts;

public sealed class GenericParameterConstraintContextTests
{
    private const string Source = """
        using System;
        namespace Fixtures;
        public class Base { }
        public interface IRelation<T, V> { }
        public class Box<T, U> where T : Base, IDisposable, IComparable<T> where U : class, new()
        {
            public void Inspect<V, W>() where W : IRelation<T, V> { }
        }
        """;

    [Fact]
    public void EnumerateConstraints_PreservesRowsTypesAndReaderWithoutDecoding()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var reader = metadata.Reader;
        var type = TypeContext.Create(reader, metadata.FindTypeDefinitionHandle("Fixtures", "Box`2"));
        var parameters = type.EnumerateGenericParameters();
        Assert.True(parameters.MoveNext());
        var parameter = parameters.Current;
        var rows = parameter.GenericParameter.GetConstraints().ToArray();
        Assert.Equal(3, parameter.ConstraintCount);
        var kinds = new[] { HandleKind.TypeDefinition, HandleKind.TypeReference, HandleKind.TypeSpecification };
        int index = 0;
        foreach (var constraint in parameter.EnumerateConstraints())
        {
            Assert.True(constraint.IsValid);
            Assert.Same(reader, constraint.Reader);
            Assert.Equal(rows[index], constraint.Handle);
            Assert.Equal(parameter.Handle, constraint.Parameter);
            Assert.Equal(reader.GetGenericParameterConstraint(rows[index]).Type, constraint.Type);
            Assert.Equal(kinds[index++], constraint.Type.Kind);
            Assert.False(constraint.EnumerateCustomAttributes().MoveNext());
        }
        Assert.Equal(rows.Length, index);

        var enumerator = parameter.EnumerateConstraints();
        Assert.True(enumerator.MoveNext());
        var copy = enumerator.GetEnumerator();
        Assert.Equal(enumerator.Current, copy.Current);
        Assert.True(copy.MoveNext());
        Assert.NotEqual(enumerator.Current, copy.Current);
        Assert.True(parameters.MoveNext());
        Assert.Equal(0, parameters.Current.ConstraintCount);
        Assert.False(parameters.Current.EnumerateConstraints().MoveNext());
    }

    [Fact]
    public void ConstraintIdentity_SameRowInDifferentReaders_RemainsDistinct()
    {
        using var first = TestUtility.BuildMetadata(Source);
        using var second = TestUtility.BuildMetadata(Source);
        var handle = GetTypeParameter(first).GenericParameter.GetConstraints().First();
        var left = GenericParameterConstraintContext.Create(first.Reader, handle);
        var same = GenericParameterConstraintContext.Create(first.Reader, handle);
        var otherReader = GenericParameterConstraintContext.Create(second.Reader, handle);
        Assert.Equal(left, same);
        Assert.True(left == same);
        Assert.True(left.Equals((object)same));
        Assert.Equal(left.GetHashCode(), same.GetHashCode());
        Assert.True(left != otherReader);
        Assert.False(default(GenericParameterConstraintContext).IsValid);
        Assert.Throws<ArgumentNullException>(() => GenericParameterConstraintContext.Create(null!, handle));
        Assert.Throws<ArgumentException>(() => GenericParameterConstraintContext.Create(first.Reader, default));
    }

    [Fact]
    public void DecodeType_DefaultModel_PreservesNamedAndGenericConstraints()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var constraints = GetTypeParameter(metadata).EnumerateConstraints();
        Assert.True(constraints.MoveNext());
        Assert.True(constraints.Current.DecodeType().Is("Fixtures", "Base"));
        Assert.True(constraints.MoveNext());
        Assert.True(constraints.Current.DecodeType().Is("System", "IDisposable"));
        Assert.True(constraints.MoveNext());
        var generic = Assert.IsType<GenericInstanceSignature>(constraints.Current.DecodeType());
        Assert.True(generic.GenericType.Is("System", "IComparable`1"));
        Assert.Single(generic.Arguments);
    }

    [Fact]
    public void DecodeType_CustomModel_ForwardsNamedHandlesAndBothGenericContexts()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var provider = new IrTypeProvider();
        var context = new IrGenericContext(
            ImmutableArray.Create<IrType>(new IrType.Primitive(PrimitiveTypeCode.String)),
            ImmutableArray.Create<IrType>(new IrType.Primitive(PrimitiveTypeCode.Int32)));
        var constraints = GetTypeParameter(metadata).EnumerateConstraints();
        Assert.True(constraints.MoveNext());
        var local = Assert.IsType<IrType.Named>(constraints.Current.DecodeType(provider, context));
        Assert.Equal("Fixtures.Base", local.Identity.FullName);
        Assert.Equal((byte)0, local.RawTypeKind);
        Assert.True(constraints.MoveNext());
        var external = Assert.IsType<IrType.Named>(constraints.Current.DecodeType(provider, context));
        Assert.Equal("System.IDisposable", external.Identity.FullName);
        Assert.Equal((byte)0, external.RawTypeKind);
        Assert.True(constraints.MoveNext());
        var generic = Assert.IsType<IrType.GenericInstance>(constraints.Current.DecodeType(provider, context));
        Assert.Equal(context.TypeArguments[0], Assert.Single(generic.Arguments));

        var methodParameter = GetMethodParameter(metadata);
        Assert.Equal("W", methodParameter.Name);
        Assert.Equal("Inspect", methodParameter.DeclaringMethod.Name);
        Assert.Equal(1, methodParameter.ConstraintCount);
        var methodConstraints = methodParameter.EnumerateConstraints();
        Assert.True(methodConstraints.MoveNext());
        var row = methodConstraints.Current;
        Assert.Equal(methodParameter.Handle, row.Parameter);
        var methodGeneric = Assert.IsType<IrType.GenericInstance>(row.DecodeType(provider, context));
        Assert.Equal(context.TypeArguments[0], methodGeneric.Arguments[0]);
        Assert.Equal(context.MethodArguments[0], methodGeneric.Arguments[1]);
        var raw = Assert.IsType<IrType.GenericInstance>(row.Constraint.DecodeConstraintTypeSignature(metadata.Reader, provider, context));
        Assert.Equal(methodGeneric.Arguments.ToArray(), raw.Arguments.ToArray());
    }

    [Fact]
    public void EnumerateCustomAttributes_ReadsAttributesOnConstraintRows()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Constraints"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        var type = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Target"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var parameter = metadata.AddGenericParameter(type, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
        var row = metadata.AddGenericParameterConstraint(parameter, type);
        var constructor = metadata.AddMemberReference(type, metadata.GetOrAddString(".ctor"),
            metadata.GetOrAddBlob(new byte[] { 0x20, 0, 1 }));
        var attribute = metadata.AddCustomAttribute(row, constructor, metadata.GetOrAddBlob(new byte[] { 1, 0, 0, 0 }));
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var owner = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
        var reader = owner.GetMetadataReader();
        var constraints = GenericParameterContext.Create(reader, parameter).EnumerateConstraints();
        Assert.True(constraints.MoveNext());
        var attributes = constraints.Current.EnumerateCustomAttributes();
        Assert.True(attributes.MoveNext());
        Assert.Same(reader, attributes.Current.Reader);
        Assert.Equal(attribute, attributes.Current.Handle);
        Assert.False(attributes.MoveNext());
    }

    [Fact]
    public void DecodeType_NullProviderOrReader_ReportsArgumentNames()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var constraints = GetTypeParameter(metadata).EnumerateConstraints();
        Assert.True(constraints.MoveNext());
        var row = constraints.Current;
        Assert.Equal("provider", Assert.Throws<ArgumentNullException>(() =>
            row.DecodeType<IrType, IrGenericContext>(null!, default)).ParamName);
        Assert.Equal("reader", Assert.Throws<ArgumentNullException>(() =>
            row.Constraint.DecodeConstraintTypeSignature(null!, new IrTypeProvider(), default(IrGenericContext))).ParamName);
    }

    [Theory]
    [InlineData(HandleKind.TypeDefinition)]
    [InlineData(HandleKind.TypeReference)]
    [InlineData(HandleKind.TypeSpecification)]
    public void DecodeType_NilConstraintType_RejectsMalformedRow(HandleKind kind)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("Constraints"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        var type = metadata.AddTypeDefinition(TypeAttributes.Public, default, metadata.GetOrAddString("Target"),
            default, MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        var parameter = metadata.AddGenericParameter(type, GenericParameterAttributes.None, metadata.GetOrAddString("T"), 0);
        EntityHandle nilType = kind switch
        {
            HandleKind.TypeDefinition => default(TypeDefinitionHandle),
            HandleKind.TypeReference => default(TypeReferenceHandle),
            HandleKind.TypeSpecification => default(TypeSpecificationHandle),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var row = metadata.AddGenericParameterConstraint(parameter, nilType);
        var image = new BlobBuilder();
        new MetadataRootBuilder(metadata).Serialize(image, 0, 0);
        using var owner = MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
        var constraint = GenericParameterConstraintContext.Create(owner.GetMetadataReader(), row);
        Assert.True(constraint.Type.IsNil);
        Assert.Equal(kind, constraint.Type.Kind);
        Assert.Throws<BadImageFormatException>(() => constraint.DecodeType(new IrTypeProvider(), default(IrGenericContext)));
    }

    [Fact]
    public void EnumerateConstraints_RepeatedRawScans_AllocateNothing()
    {
        using var metadata = TestUtility.BuildMetadata(Source);
        var parameter = GetTypeParameter(metadata);
        int expected = Scan(parameter);
        for (int i = 0; i < 10_000; i++)
        {
            Scan(parameter);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 20_000; i++)
        {
            sum += Scan(parameter);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(expected * 20_000, sum);
        Assert.Equal(0, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Scan(GenericParameterContext parameter)
    {
        int sum = parameter.ConstraintCount;
        foreach (var constraint in parameter.EnumerateConstraints())
        {
            sum += MetadataTokens.GetRowNumber(constraint.Handle);
            sum += MetadataTokens.GetRowNumber(constraint.Type);
            sum += MetadataTokens.GetRowNumber(constraint.Parameter);
        }
        return sum;
    }

    private static GenericParameterContext GetTypeParameter(MetadataScope metadata)
    {
        var type = metadata.GetTypeDefinition("Fixtures", "Box`2");
        return GenericParameterContext.Create(metadata.Reader, type.GetGenericParameters().First());
    }

    private static GenericParameterContext GetMethodParameter(MetadataScope metadata)
    {
        var type = metadata.GetTypeDefinition("Fixtures", "Box`2");
        var method = type.GetMethods().Single(handle => metadata.Reader.StringComparer.Equals(metadata.Reader.GetMethodDefinition(handle).Name, "Inspect"));
        var parameter = metadata.Reader.GetMethodDefinition(method).GetGenericParameters().Last();
        return GenericParameterContext.Create(metadata.Reader, parameter);
    }
}

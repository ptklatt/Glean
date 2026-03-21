using System.Collections.Immutable;
using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class GenericParameterExtensionsTests
{
    private const string GenericParameterSource = """
        using System;
        using System.Collections.Generic;

        namespace Glean.Tests.Extensions;

        internal interface CovariantFixture<out T>
        {
        }

        internal interface ContravariantFixture<in T>
        {
        }

        internal interface InvariantFixture<T>
        {
        }

        internal sealed class GenericParameterFixture<TReference, TValue, TPlain>
            where TReference : class, IDisposable, IEnumerable<int>, new()
            where TValue : struct
        {
            internal void MethodWithConstraint<TMethod>()
                where TMethod : class, IComparable<int>, new()
            {
            }
        }
        """;

    private const string GenericParameterContextSource = """
        using System.Collections.Generic;

        namespace Glean.Tests.Extensions;

        internal sealed class GenericContextFixture<TOuter>
        {
            internal void MethodWithSubstitutedConstraint<TMethod>()
                where TMethod : IEnumerable<TOuter>
            {
            }
        }
        """;

    // == Variance flags ======================================================

    [Fact]
    public void VarianceFlags_InterfaceGenericParameters_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(GenericParameterSource);

        var covariant = GetTypeGenericParameter(metadata, "Glean.Tests.Extensions", "CovariantFixture`1", "T");
        var contravariant = GetTypeGenericParameter(metadata, "Glean.Tests.Extensions", "ContravariantFixture`1", "T");
        var invariant = GetTypeGenericParameter(metadata, "Glean.Tests.Extensions", "InvariantFixture`1", "T");

        Assert.True(covariant.IsCovariant());
        Assert.False(covariant.IsContravariant());
        Assert.False(covariant.IsInvariant());

        Assert.True(contravariant.IsContravariant());
        Assert.False(contravariant.IsCovariant());
        Assert.False(contravariant.IsInvariant());

        Assert.True(invariant.IsInvariant());
        Assert.False(invariant.IsCovariant());
        Assert.False(invariant.IsContravariant());
    }

    // == Constraint flags ====================================================

    [Fact]
    public void ConstraintFlags_TypeGenericParameters_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(GenericParameterSource);

        var referenceParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "TReference");
        var valueParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "TValue");
        var plainParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "TPlain");

        Assert.True(referenceParameter.HasReferenceTypeConstraint());
        Assert.True(referenceParameter.HasDefaultConstructorConstraint());
        Assert.True(referenceParameter.HasSpecialConstraint());
        Assert.False(referenceParameter.HasNotNullableValueTypeConstraint());

        Assert.True(valueParameter.HasNotNullableValueTypeConstraint());
        Assert.True(valueParameter.HasSpecialConstraint());
        Assert.False(valueParameter.HasReferenceTypeConstraint());

        Assert.False(plainParameter.HasReferenceTypeConstraint());
        Assert.False(plainParameter.HasNotNullableValueTypeConstraint());
        Assert.False(plainParameter.HasDefaultConstructorConstraint());
        Assert.False(plainParameter.HasSpecialConstraint());
    }

    // == Metadata access =====================================================

    [Fact]
    public void MetadataAccess_NameAndConstraintHandles_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(GenericParameterSource);

        var typeParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "TReference");
        var methodParameter = GetMethodGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "MethodWithConstraint",
            "TMethod");

        Assert.True(typeParameter.NameIs(metadata.Reader, "TReference"));
        Assert.False(typeParameter.NameIs(metadata.Reader, "TValue"));
        Assert.Equal(2, CountConstraintHandles(typeParameter.GetConstraintHandles()));

        Assert.True(methodParameter.NameIs(metadata.Reader, "TMethod"));
        Assert.False(methodParameter.NameIs(metadata.Reader, "TReference"));
        Assert.Equal(1, CountConstraintHandles(methodParameter.GetConstraintHandles()));
    }

    [Fact]
    public void MetadataAccess_ConstraintSignatures_ReportExpectedDecodedTypes()
    {
        using var metadata = TestUtility.BuildMetadata(GenericParameterSource);

        var typeParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "TReference");
        var enumerator = typeParameter.GetConstraintSignatures(metadata.Reader);

        Assert.True(enumerator.MoveNext());
        var first = Assert.IsType<TypeReferenceSignature>(enumerator.Current);
        Assert.True(first.Is("System", "IDisposable"));

        Assert.True(enumerator.MoveNext());
        var second = Assert.IsType<GenericInstanceSignature>(enumerator.Current);
        Assert.True(second.Is("System.Collections.Generic", "IEnumerable`1"));

        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(second.Arguments));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MetadataAccess_ConstraintTypes_ReportExpectedHandlesAndConstraintRows()
    {
        using var metadata = TestUtility.BuildMetadata(GenericParameterSource);
        var reader = metadata.Reader;

        var typeParameter = GetTypeGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericParameterFixture`3",
            "TReference");
        var enumerator = typeParameter.GetConstraintTypes(reader);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(HandleKind.TypeReference, enumerator.Current.Kind);
        Assert.Equal(enumerator.Current,
                     reader.GetGenericParameterConstraint(enumerator.ConstraintHandle).Type);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(HandleKind.TypeSpecification, enumerator.Current.Kind);
        Assert.Equal(enumerator.Current,
                     reader.GetGenericParameterConstraint(enumerator.ConstraintHandle).Type);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MetadataAccess_ConstraintSignaturesWithCustomContext_SubstitutesTypeArguments()
    {
        using var metadata = TestUtility.BuildMetadata(GenericParameterContextSource);

        var methodParameter = GetMethodGenericParameter(
            metadata,
            "Glean.Tests.Extensions",
            "GenericContextFixture`1",
            "MethodWithSubstitutedConstraint",
            "TMethod");
        var int32 = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var context = new SignatureDecodeContext(typeArguments: ImmutableArray.Create<TypeSignature>(int32));
        var enumerator = methodParameter.GetConstraintSignatures(
            metadata.Reader,
            SignatureTypeProvider.Instance,
            context);

        Assert.True(enumerator.MoveNext());
        var constraint = Assert.IsType<GenericInstanceSignature>(enumerator.Current);
        Assert.True(constraint.Is("System.Collections.Generic", "IEnumerable`1"));
        Assert.Same(int32, Assert.Single(constraint.Arguments));
        Assert.False(enumerator.MoveNext());
    }

    private static GenericParameter GetTypeGenericParameter(
        MetadataScope metadata,
        string ns,
        string typeName,
        string parameterName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        return GetGenericParameter(metadata.Reader, typeDefinition.GetGenericParameters(), parameterName);
    }

    private static GenericParameter GetMethodGenericParameter(
        MetadataScope metadata,
        string ns,
        string typeName,
        string methodName,
        string parameterName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        var methodDefinition = GetMethodDefinition(metadata.Reader, typeDefinition, methodName);
        return GetGenericParameter(metadata.Reader, methodDefinition.GetGenericParameters(), parameterName);
    }

    private static MethodDefinition GetMethodDefinition(
        MetadataReader reader,
        TypeDefinition typeDefinition,
        string methodName)
    {
        foreach (var handle in typeDefinition.GetMethods())
        {
            var methodDefinition = reader.GetMethodDefinition(handle);
            if (methodDefinition.NameIs(reader, methodName))
            {
                return methodDefinition;
            }
        }

        throw new InvalidOperationException($"Could not locate method {methodName}.");
    }

    private static GenericParameter GetGenericParameter(
        MetadataReader reader,
        GenericParameterHandleCollection handles,
        string parameterName)
    {
        foreach (var handle in handles)
        {
            var genericParameter = reader.GetGenericParameter(handle);
            if (genericParameter.NameIs(reader, parameterName))
            {
                return genericParameter;
            }
        }

        throw new InvalidOperationException($"Could not locate generic parameter {parameterName}.");
    }

    private static int CountConstraintHandles(GenericParameterConstraintHandleCollection handles)
    {
        int count = 0;
        foreach (var _ in handles)
        {
            count++;
        }

        return count;
    }
}

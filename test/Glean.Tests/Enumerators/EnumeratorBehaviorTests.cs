using System.Collections.Generic;
using System.Reflection.Metadata;

using Xunit;

using Glean.Contexts;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Enumerators;

public class EnumeratorBehaviorTests
{
    private const string InterfaceEnumerationSource = """
        namespace Glean.Tests.Enumerators
        {
            internal interface ISimpleInterfaceFixture
            {
            }

            internal interface IGenericInterfaceFixture<T>
            {
            }

            internal sealed class InterfaceEnumerationFixture : ISimpleInterfaceFixture, IGenericInterfaceFixture<int>
            {
            }
        }
        """;

    private const string NestedAndParameterSource = """
        namespace Glean.Tests.Enumerators
        {
            internal sealed class OuterEnumerationFixture
            {
                internal sealed class FirstNestedFixture
                {
                }

                private sealed class SecondNestedFixture
                {
                }

                public void Sample(int value, string name)
                {
                }
            }
        }
        """;

    private const string MethodImplementationSource = """
        namespace Glean.Tests.Enumerators
        {
            internal interface IMethodImplementationFixture
            {
                void Run();
            }

            internal sealed class MethodImplementationFixture : IMethodImplementationFixture
            {
                void IMethodImplementationFixture.Run()
                {
                }
            }
        }
        """;

    // == Interface Enumeration ================================================

    [Fact]
    public void EnumerateInterfaces_GenericFixture_ReturnsDecodedSignaturesInMetadataOrder()
    {
        using var metadata = TestUtility.BuildMetadata(InterfaceEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "InterfaceEnumerationFixture");

        Assert.Equal(2, type.InterfaceCount);

        var enumerator = type.EnumerateInterfaces();

        Assert.True(enumerator.MoveNext());
        var first = Assert.IsAssignableFrom<TypeSignature>(enumerator.Current);
        Assert.True(first.Is("Glean.Tests.Enumerators", "ISimpleInterfaceFixture"));

        Assert.True(enumerator.MoveNext());
        var second = Assert.IsType<GenericInstanceSignature>(enumerator.Current);
        Assert.True(second.Is("Glean.Tests.Enumerators", "IGenericInterfaceFixture`1"));
        Assert.Single(second.Arguments);

        var argument = Assert.IsType<PrimitiveTypeSignature>(second.Arguments[0]);
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void EnumerateInterfaceTypes_GenericFixture_ReturnsRawInterfaceHandles()
    {
        using var metadata = TestUtility.BuildMetadata(InterfaceEnumerationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "InterfaceEnumerationFixture");
        var reader = metadata.Reader;

        var enumerator = type.EnumerateInterfaceTypes();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(HandleKind.TypeDefinition, enumerator.Current.Kind);
        Assert.Equal(
            enumerator.Current,
            reader.GetInterfaceImplementation(enumerator.InterfaceImplementationHandle).Interface);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(HandleKind.TypeSpecification, enumerator.Current.Kind);
        Assert.Equal(
            enumerator.Current,
            reader.GetInterfaceImplementation(enumerator.InterfaceImplementationHandle).Interface);

        Assert.False(enumerator.MoveNext());
    }

    // == Parameter Enumeration ================================================

    [Fact]
    public void EnumerateParameters_MethodFixture_PreservesDeclaringMethodContext()
    {
        using var metadata = TestUtility.BuildMetadata(NestedAndParameterSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "OuterEnumerationFixture");
        var method = GetMethodContext(type, "Sample");

        Assert.Equal(2, type.MethodCount);
        Assert.Equal(0, type.PropertyCount);
        Assert.Equal(0, type.EventCount);

        var enumerator = method.EnumerateParameters();
        var names = new List<string>();
        var sequenceNumbers = new List<int>();

        while (enumerator.MoveNext())
        {
            var parameter = enumerator.Current;
            names.Add(parameter.Name);
            sequenceNumbers.Add(parameter.SequenceNumber);
            Assert.Equal(method.Handle, parameter.DeclaringMethod.Handle);
            Assert.Equal("Sample", parameter.DeclaringMethod.Name);
        }

        Assert.Equal(new[] { "value", "name" }, names);
        Assert.Equal(new[] { 1, 2 }, sequenceNumbers);
    }

    // == Method Implementation Enumeration ====================================

    [Fact]
    public void EnumerateMethodImplementations_ExplicitImplementation_ReturnsMethodBodyAndDeclaration()
    {
        using var metadata = TestUtility.BuildMetadata(MethodImplementationSource);
        var type = GetTypeContext(metadata, "Glean.Tests.Enumerators", "MethodImplementationFixture");
        var reader = metadata.Reader;

        var enumerator = type.EnumerateMethodImplementations();

        Assert.True(enumerator.MoveNext());
        var implementation = enumerator.Current;

        Assert.Equal(type.Handle, implementation.Type);
        Assert.Equal(HandleKind.MethodDefinition, implementation.MethodBody.Kind);
        Assert.Equal(HandleKind.MethodDefinition, implementation.MethodDeclaration.Kind);

        var body = reader.GetMethodDefinition((MethodDefinitionHandle)implementation.MethodBody);
        var declaration = reader.GetMethodDefinition((MethodDefinitionHandle)implementation.MethodDeclaration);

        Assert.EndsWith("Run", reader.GetString(body.Name));
        Assert.Equal("Run", reader.GetString(declaration.Name));
        Assert.False(enumerator.MoveNext());
    }

    // == Nested Type Enumeration ==============================================

    [Fact]
    public void EnumerateNestedTypes_OuterFixture_ReturnsNestedTypesInMetadataOrder()
    {
        using var metadata = TestUtility.BuildMetadata(NestedAndParameterSource);
        var outerType = GetTypeContext(metadata, "Glean.Tests.Enumerators", "OuterEnumerationFixture");

        var enumerator = outerType.EnumerateNestedTypes();
        var names = new List<string>();

        while (enumerator.MoveNext())
        {
            var nested = enumerator.Current;
            names.Add(nested.Name);
            Assert.Equal(outerType.Handle, nested.GetDeclaringType().Handle);
        }

        Assert.Equal(new[] { "FirstNestedFixture", "SecondNestedFixture" }, names);
    }

    private static TypeContext GetTypeContext(MetadataScope metadata, string ns, string name)
    {
        var handle = metadata.FindTypeDefinitionHandle(ns, name);
        if (handle.IsNil)
        {
            throw new InvalidOperationException($"Could not locate {ns}.{name}.");
        }

        return TypeContext.Create(metadata.Reader, handle);
    }

    private static MethodContext GetMethodContext(TypeContext type, string methodName)
    {
        var enumerator = type.EnumerateMethods();
        while (enumerator.MoveNext())
        {
            var method = enumerator.Current;
            if (method.NameIs(methodName))
            {
                return method;
            }
        }

        throw new InvalidOperationException($"Could not locate method {methodName} on {type.FullName}.");
    }
}

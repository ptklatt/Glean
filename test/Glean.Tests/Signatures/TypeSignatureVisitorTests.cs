using System.Collections.Immutable;
using System.Reflection.Metadata;

using Xunit;

using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Signatures;

public sealed class TypeSignatureVisitorTests
{
    [Fact]
    public void Accept_DispatchesEachConcreteSignature()
    {
        using var coreLib = TestUtility.OpenCoreLibMetadata();
        using var glean = TestUtility.OpenMetadata(typeof(TypeSignature).Assembly);

        var typeDefinitionHandle = coreLib.FindTypeDefinitionHandle("System", "Int32");
        var typeReferenceHandle = glean.FindTypeReferenceHandle("System", "Object");

        Assert.False(typeDefinitionHandle.IsNil);
        Assert.False(typeReferenceHandle.IsNil);

        var functionPointer = new FunctionPointerSignature(
            new MethodSignature<TypeSignature>(
                header: default,
                returnType: PrimitiveTypeSignature.Get(PrimitiveTypeCode.Void),
                requiredParameterCount: 1,
                genericParameterCount: 0,
                parameterTypes: ImmutableArray.Create<TypeSignature>(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32))));

        var signatures = new (TypeSignature Signature, string Expected)[]
        {
            (new ArraySignature(
                PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32),
                new ArrayShape(rank: 2, sizes: ImmutableArray<int>.Empty, lowerBounds: ImmutableArray<int>.Empty)), "state:Array"),
            (new ByRefSignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.String)), "state:ByRef"),
            (functionPointer, "state:FunctionPointer"),
            (new GenericInstanceSignature(
                PrimitiveTypeSignature.Get(PrimitiveTypeCode.Object),
                ImmutableArray.Create<TypeSignature>(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32))), "state:GenericInstance"),
            (new GenericMethodParameterSignature(2), "state:GenericMethodParameter"),
            (new GenericTypeParameterSignature(1), "state:GenericTypeParameter"),
            (new ModifiedTypeSignature(
                PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32),
                requiredModifiers: ImmutableArray<TypeSignature>.Empty,
                optionalModifiers: ImmutableArray<TypeSignature>.Empty), "state:ModifiedType"),
            (new PinnedTypeSignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Byte)), "state:PinnedType"),
            (new PointerSignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Char)), "state:Pointer"),
            (PrimitiveTypeSignature.Get(PrimitiveTypeCode.Boolean), "state:Primitive"),
            (new SerializedTypeNameSignature("Example.Widget"), "state:SerializedTypeName"),
            (SentinelTypeSignature.Instance, "state:Sentinel"),
            (new SZArraySignature(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Double)), "state:SZArray"),
            (new TypeDefinitionSignature(coreLib.Reader, typeDefinitionHandle), "state:TypeDefinition"),
            (new TypeReferenceSignature(glean.Reader, typeReferenceHandle), "state:TypeReference"),
        };

        var visitor = new DispatchVisitor();

        foreach (var (signature, expected) in signatures)
        {
            signature.Accept(ref visitor, "state");
            Assert.Equal(expected, visitor.LastResult);
        }

        Assert.Equal(signatures.Length, visitor.VisitCount);
    }

    [Fact]
    public void Accept_SupportsRecursiveProjectionOverNestedSignatureGraphs()
    {
        using var coreLib = TestUtility.OpenCoreLibMetadata();
        using var glean = TestUtility.OpenMetadata(typeof(TypeSignature).Assembly);

        var int32Handle = coreLib.FindTypeDefinitionHandle("System", "Int32");
        var objectHandle = glean.FindTypeReferenceHandle("System", "Object");

        Assert.False(int32Handle.IsNil);
        Assert.False(objectHandle.IsNil);

        TypeSignature signature = new GenericInstanceSignature(
            new TypeReferenceSignature(glean.Reader, objectHandle),
            ImmutableArray.Create<TypeSignature>(
                new SZArraySignature(new TypeDefinitionSignature(coreLib.Reader, int32Handle)),
                new PointerSignature(new SerializedTypeNameSignature("Example.Widget"))));

        var visitor = new NamedTypeCountVisitor();
        signature.Accept(ref visitor, state: (object?)null);

        Assert.Equal(3, visitor.Count);
    }

    private struct DispatchVisitor : ITypeSignatureVisitor<string>
    {
        public int VisitCount;
        public string? LastResult;

        public void VisitArray(ArraySignature signature, string state)
        {
            Record(state, "Array");
        }

        public void VisitByRef(ByRefSignature signature, string state)
        {
            Record(state, "ByRef");
        }

        public void VisitFunctionPointer(FunctionPointerSignature signature, string state)
        {
            Record(state, "FunctionPointer");
        }

        public void VisitGenericInstance(GenericInstanceSignature signature, string state)
        {
            Record(state, "GenericInstance");
        }

        public void VisitGenericMethodParameter(GenericMethodParameterSignature signature, string state)
        {
            Record(state, "GenericMethodParameter");
        }

        public void VisitGenericTypeParameter(GenericTypeParameterSignature signature, string state)
        {
            Record(state, "GenericTypeParameter");
        }

        public void VisitModifiedType(ModifiedTypeSignature signature, string state)
        {
            Record(state, "ModifiedType");
        }

        public void VisitPinnedType(PinnedTypeSignature signature, string state)
        {
            Record(state, "PinnedType");
        }

        public void VisitPointer(PointerSignature signature, string state)
        {
            Record(state, "Pointer");
        }

        public void VisitPrimitive(PrimitiveTypeSignature signature, string state)
        {
            Record(state, "Primitive");
        }

        public void VisitSerializedTypeName(SerializedTypeNameSignature signature, string state)
        {
            Record(state, "SerializedTypeName");
        }

        public void VisitSentinel(SentinelTypeSignature signature, string state)
        {
            Record(state, "Sentinel");
        }

        public void VisitSZArray(SZArraySignature signature, string state)
        {
            Record(state, "SZArray");
        }

        public void VisitTypeDefinition(TypeDefinitionSignature signature, string state)
        {
            Record(state, "TypeDefinition");
        }

        public void VisitTypeReference(TypeReferenceSignature signature, string state)
        {
            Record(state, "TypeReference");
        }

        private void Record(string state, string kind)
        {
            VisitCount++;
            LastResult = $"{state}:{kind}";
        }
    }

    private sealed class NamedTypeCountVisitor : ITypeSignatureVisitor<object?>
    {
        public int Count { get; private set; }

        public void VisitArray(ArraySignature signature, object? state)
        {
            VisitChild(signature.ElementType);
        }

        public void VisitByRef(ByRefSignature signature, object? state)
        {
            VisitChild(signature.ElementType);
        }

        public void VisitFunctionPointer(FunctionPointerSignature signature, object? state)
        {
            VisitChild(signature.Signature.ReturnType);
            foreach (var parameterType in signature.Signature.ParameterTypes)
            {
                VisitChild(parameterType);
            }
        }

        public void VisitGenericInstance(GenericInstanceSignature signature, object? state)
        {
            VisitChild(signature.GenericType);
            foreach (var argument in signature.Arguments)
            {
                VisitChild(argument);
            }
        }

        public void VisitGenericMethodParameter(GenericMethodParameterSignature signature, object? state)
        {
        }

        public void VisitGenericTypeParameter(GenericTypeParameterSignature signature, object? state)
        {
        }

        public void VisitModifiedType(ModifiedTypeSignature signature, object? state)
        {
            VisitChild(signature.UnmodifiedType);
            foreach (var modifier in signature.RequiredModifiers)
            {
                VisitChild(modifier);
            }

            foreach (var modifier in signature.OptionalModifiers)
            {
                VisitChild(modifier);
            }
        }

        public void VisitPinnedType(PinnedTypeSignature signature, object? state)
        {
            VisitChild(signature.ElementType);
        }

        public void VisitPointer(PointerSignature signature, object? state)
        {
            VisitChild(signature.ElementType);
        }

        public void VisitPrimitive(PrimitiveTypeSignature signature, object? state)
        {
        }

        public void VisitSerializedTypeName(SerializedTypeNameSignature signature, object? state)
        {
            Count++;
        }

        public void VisitSentinel(SentinelTypeSignature signature, object? state)
        {
        }

        public void VisitSZArray(SZArraySignature signature, object? state)
        {
            VisitChild(signature.ElementType);
        }

        public void VisitTypeDefinition(TypeDefinitionSignature signature, object? state)
        {
            Count++;
        }

        public void VisitTypeReference(TypeReferenceSignature signature, object? state)
        {
            Count++;
        }

        private void VisitChild(TypeSignature signature)
        {
            var self = this;
            signature.Accept(ref self, state: (object?)null);
        }
    }
}
